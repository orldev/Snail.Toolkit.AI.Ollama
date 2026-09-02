using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Transport-level tests for <see cref="ChatClient"/> against a stubbed HTTP handler
/// speaking Ollama's native NDJSON protocol.
/// </summary>
public class ChatClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return responder(request);
        }
    }

    private static ChatClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions { DefaultModel = "test-model" }));

    private static HttpResponseMessage CreateNdjsonResponse(params string[] lines) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent($"{string.Join("\n", lines)}\n", Encoding.UTF8, "application/x-ndjson")
        };

    private static HttpResponseMessage CreateJsonResponse(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetStreamingResponseAsync_TextStream_YieldsTextAndFinalUsage()
    {
        var handler = new StubHandler(_ => CreateNdjsonResponse(
            """{"model":"m","message":{"role":"assistant","content":"Hel"},"done":false}""",
            """{"model":"m","message":{"role":"assistant","content":"lo"},"done":false}""",
            """{"model":"m","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":10,"eval_count":5}"""));
        using var client = CreateClient(handler);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
        {
            updates.Add(update);
        }

        Assert.Equal("Hello", string.Concat(updates.Select(u => u.Text)));
        Assert.Equal(ChatFinishReason.Stop, updates[^1].FinishReason);
        var usage = Assert.Single(updates[^1].Contents.OfType<UsageContent>());
        Assert.Equal(15, usage.Details.TotalTokenCount);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_ContentAfterDoneChunk_StopsAtDoneChunk()
    {
        var handler = new StubHandler(_ => CreateNdjsonResponse(
            """{"model":"m","message":{"role":"assistant","content":"Hi"},"done":false}""",
            """{"model":"m","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop"}""",
            "this-is-not-json"));
        using var client = CreateClient(handler);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
        {
            updates.Add(update);
        }

        Assert.Equal(2, updates.Count);
    }

    [Fact]
    public async Task GetResponseAsync_ValidRequest_DisablesStreamingAndMapsResponse()
    {
        var handler = new StubHandler(_ => CreateJsonResponse(
            """{"model":"m","message":{"role":"assistant","content":"Hi!"},"done":true,"done_reason":"stop","prompt_eval_count":7,"eval_count":3}"""));
        using var client = CreateClient(handler);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("Hi!", response.Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(10, response.Usage!.TotalTokenCount);

        var body = JsonDocument.Parse(handler.RequestBodies.Single()).RootElement;
        Assert.False(body.GetProperty("stream").GetBoolean());
        Assert.Equal("test-model", body.GetProperty("model").GetString());
    }

    [Fact]
    public async Task GetResponseAsync_ErrorStatus_SurfacesErrorBody()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"error":"model 'missing' not found"}""", Encoding.UTF8, "application/json")
        });
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<HttpBuilderException>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.Contains("model 'missing' not found", exception.Body);
    }

    [Fact]
    public async Task BufferTextAsync_SmallUpdates_CoalescesIntoSegments()
    {
        var handler = new StubHandler(_ => CreateNdjsonResponse(
            """{"model":"m","message":{"role":"assistant","content":"Hel"},"done":false}""",
            """{"model":"m","message":{"role":"assistant","content":"lo "},"done":false}""",
            """{"model":"m","message":{"role":"assistant","content":"there"},"done":false}""",
            """{"model":"m","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop"}"""));
        using var client = CreateClient(handler);

        var segments = new List<string>();
        await foreach (var segment in client
                           .GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")])
                           .BufferTextAsync(chunkSize: 6))
        {
            segments.Add(segment);
        }

        Assert.Equal(["Hello ", "there"], segments);
    }

    [Fact]
    public void GetService_MetadataRequested_ReturnsChatClientMetadata()
    {
        using var client = CreateClient(new StubHandler(_ => CreateJsonResponse("{}")));

        var metadata = Assert.IsType<ChatClientMetadata>(client.GetService(typeof(ChatClientMetadata)));
        Assert.Equal("ollama", metadata.ProviderName);
        Assert.Equal("test-model", metadata.DefaultModelId);
    }

    [Fact]
    public async Task GetResponseAsync_NoModelConfigured_ThrowsInvalidOperationException()
    {
        var handler = new StubHandler(_ => CreateJsonResponse("{}"));
        using var client = new ChatClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions()));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));

        Assert.Contains("model", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.RequestBodies);
    }

    [Fact]
    public async Task GetResponseAsync_NativeToolCallLoop_InvokesFunctionAndCompletes()
    {
        var responses = new Queue<HttpResponseMessage>([
            CreateJsonResponse("""{"model":"m","message":{"role":"assistant","content":"","tool_calls":[{"function":{"name":"get_current_weather","arguments":{"location":"Paris"}}}]},"done":true,"done_reason":"stop","prompt_eval_count":100,"eval_count":20}"""),
            CreateJsonResponse("""{"model":"m","message":{"role":"assistant","content":"It is 18C in Paris."},"done":true,"done_reason":"stop","prompt_eval_count":150,"eval_count":25}""")
        ]);
        var handler = new StubHandler(_ => responses.Dequeue());

        var invoked = false;
        var getWeather = AIFunctionFactory.Create(
            (string location) =>
            {
                invoked = true;
                return $"18C in {location}";
            },
            "get_current_weather", "Gets the current weather");

        using var inner = CreateClient(handler);
        using var client = new ChatClientBuilder(inner).UseFunctionInvocation().Build();

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Weather in Paris?")],
            new ChatOptions { Tools = [getWeather] });

        Assert.True(invoked);
        Assert.Contains("18C", response.Text);

        var secondBody = JsonDocument.Parse(handler.RequestBodies[1]).RootElement;
        var toolMessage = secondBody.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("role").GetString() == "tool");
        Assert.Equal("get_current_weather", toolMessage.GetProperty("tool_name").GetString());
        Assert.Contains("18C", toolMessage.GetProperty("content").GetString());
    }
}
