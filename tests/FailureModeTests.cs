using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// How the clients behave when Ollama, the network or the caller's input goes wrong.
/// </summary>
public class FailureModeTests
{
    private const string Reply = """{"model":"m","created_at":"2026-01-01T00:00:00Z","message":{"role":"assistant","content":"ok"},"done":true,"done_reason":"stop"}""";

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<string>> respond) : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        public List<string> RequestPaths { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = await request.Content.ReadAsSentAsync(cancellationToken);

            lock (RequestBodies)
            {
                RequestBodies.Add(body);
                RequestPaths.Add(request.RequestUri!.AbsolutePath);
            }

            string response = await respond(request, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/x-ndjson")
            };
        }
    }

    /// <summary>
    /// Answers 200 with a body that holds what was written to the pipe and never ends.
    /// </summary>
    private sealed class Unending(Pipe pipe) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(pipe.Reader.AsStream()) });
    }

    private static StubHandler Answering(string response) => new((_, _) => Task.FromResult(response));

    private static ChatClient Chat(StubHandler handler, string baseUrl = "http://localhost:11434", TimeSpan? timeout = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(baseUrl) },
            Options.Create(new OllamaOptions { DefaultModel = "m", Timeout = timeout ?? TimeSpan.FromSeconds(100) }));

    private static GenerateClient Generate(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") });

    /// <summary>
    /// Ollama reports a failure that happens after the 200 headers as an {"error": ...} line inside the
    /// stream. Read as an ordinary chunk it has no text and no done flag, so the stream just ends and the
    /// caller receives a truncated answer as if it were complete.
    /// </summary>
    [Fact]
    public async Task GenerateStream_ErrorLineMidStream_Throws()
    {
        var client = Generate(Answering("""
            {"model":"m","response":"Once upon","done":false}
            {"error":"llama runner process has terminated"}
            """));

        var failure = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in client.StreamAsync(new GenerateRequest("m", "story"))) { }
        });

        Assert.Contains("llama runner process has terminated", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same mid-stream error on /api/chat fails deserialization of the required message, so the caller
    /// sees a JSON error about a missing property instead of what Ollama actually said.
    /// </summary>
    [Fact]
    public async Task ChatStream_ErrorLineMidStream_SurfacesOllamaMessage()
    {
        var client = Chat(Answering("""
            {"model":"m","created_at":"2026-01-01T00:00:00Z","message":{"role":"assistant","content":"Once"},"done":false}
            {"error":"llama runner process has terminated"}
            """));

        var failure = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")])) { }
        });

        Assert.Contains("llama runner process has terminated", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A proxy or a crashed runner can close the stream cleanly before the final chunk. Without a done
    /// chunk there is no finish reason and no usage, and the caller cannot tell the answer was cut.
    /// </summary>
    [Fact]
    public async Task ChatStream_EndsWithoutDoneChunk_Throws()
    {
        var client = Chat(Answering("""
            {"model":"m","created_at":"2026-01-01T00:00:00Z","message":{"role":"assistant","content":"Once"},"done":false}
            """));

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")])) { }
        });
    }

    /// <summary>
    /// Endpoints written as "/api/chat" are absolute paths, so they replace the base address path instead of
    /// extending it: Ollama published under a proxy prefix is unreachable.
    /// </summary>
    [Fact]
    public async Task Chat_BaseUrlWithPathPrefix_KeepsThePrefix()
    {
        var handler = Answering(Reply);
        var client = Chat(handler, baseUrl: "https://gateway.example/ollama/");

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("/ollama/api/chat", handler.RequestPaths.Single());
    }

    /// <summary>
    /// MEAI carries the seed as a long; narrowing it to int wraps silently, so a seed above int.MaxValue
    /// reproduces a different, unrelated generation.
    /// </summary>
    [Fact]
    public async Task Chat_SeedAboveIntMaxValue_ReachesOllamaUnchanged()
    {
        var handler = Answering(Reply);
        var client = Chat(handler);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { Seed = 5_000_000_000 });

        long seed = JsonDocument.Parse(handler.RequestBodies.Single()).RootElement
            .GetProperty("options").GetProperty("seed").GetInt64();
        Assert.Equal(5_000_000_000, seed);
    }

    /// <summary>
    /// The unary timeout cancels a linked token, so its expiry surfaces as the same cancellation the caller
    /// would see after cancelling on purpose; a TimeoutException tells the two apart.
    /// </summary>
    [Fact]
    public async Task Chat_UnaryTimeoutExpires_ThrowsTimeoutException()
    {
        var client = Chat(new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Reply;
        }), timeout: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TimeoutException>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]));
    }

    /// <summary>
    /// A cancelled caller must still see cancellation, not a timeout.
    /// </summary>
    [Fact]
    public async Task Chat_CallerCancels_ThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var client = Chat(new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Reply;
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: cancellation.Token));
    }

    /// <summary>
    /// One typed client instance is shared by every caller of a scope; concurrent calls must not bleed a
    /// body or a model into one another.
    /// </summary>
    [Fact]
    public async Task Chat_TwoHundredConcurrentCalls_KeepEveryRequestApart()
    {
        var handler = Answering(Reply);
        var client = Chat(handler);

        var calls = Enumerable.Range(0, 200)
            .Select(index => client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, $"question {index}")], new ChatOptions { ModelId = $"model-{index}" }));
        await Task.WhenAll(calls);

        var sent = handler.RequestBodies
            .Select(body => JsonDocument.Parse(body).RootElement)
            .Select(root => (Model: root.GetProperty("model").GetString(),
                             Text: root.GetProperty("messages")[0].GetProperty("content").GetString()))
            .ToList();
        Assert.Equal(200, sent.Distinct().Count());
        Assert.All(sent, pair => Assert.Equal($"question {pair.Model!["model-".Length..]}", pair.Text));
    }

    /// <summary>
    /// Windows line ends and blank keep-alive lines between chunks must neither end the stream nor fail it.
    /// </summary>
    [Fact]
    public async Task ChatStream_CrlfAndBlankLines_ReadsEveryChunk()
    {
        string stream = "{\"model\":\"m\",\"created_at\":\"2026-01-01T00:00:00Z\",\"message\":{\"role\":\"assistant\",\"content\":\"a\"},\"done\":false}\r\n\r\n   \n"
                        + Reply + "\r\n";
        var client = Chat(Answering(stream));

        List<string> texts = [];

        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
        {
            texts.Add(update.Text);
        }

        Assert.Equal(["a", "ok"], texts);
    }

    /// <summary>
    /// A caller who stops waiting mid-generation must see a cancellation, not the IOException of a broken stream.
    /// </summary>
    [Fact]
    public async Task ChatStream_CallerCancelsMidStream_ThrowsOperationCanceled()
    {
        var pipe = new Pipe();
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("{\"model\":\"m\",\"created_at\":\"2026-01-01T00:00:00Z\",\"message\":{\"role\":\"assistant\",\"content\":\"a\"},\"done\":false}\n"));
        var streaming = new ChatClient(
            new HttpClient(new Unending(pipe)) { BaseAddress = new Uri("http://localhost:11434/") },
            Options.Create(new OllamaOptions { DefaultModel = "m" }));
        using var stop = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in streaming.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: stop.Token))
            {
                await stop.CancelAsync();
            }
        });
    }
}
