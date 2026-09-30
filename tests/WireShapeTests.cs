using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// What the clients put on the wire: only the fields the caller set, named as Ollama documents them.
/// </summary>
public class WireShapeTests
{
    private const string Reply = """{"model":"m","created_at":"2026-01-01T00:00:00Z","message":{"role":"assistant","content":"ok"},"done":true,"done_reason":"stop"}""";

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public JsonElement Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private static (ChatClient Client, RecordingHandler Handler) Chat(string response = Reply)
    {
        var handler = new RecordingHandler(response);
        var client = new ChatClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions { DefaultModel = "m" }));

        return (client, handler);
    }

    private static IEnumerable<string> Keys(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name);

    [Fact]
    public async Task Chat_PlainQuestion_SendsNoUnsetFields()
    {
        var (client, handler) = Chat();

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.DoesNotContain("format", Keys(handler.Body));
        Assert.DoesNotContain("options", Keys(handler.Body));
        Assert.DoesNotContain("tools", Keys(handler.Body));
        Assert.Equal(["role", "content"], Keys(handler.Body.GetProperty("messages")[0]));
    }

    [Fact]
    public async Task Chat_OnlyTemperatureSet_SendsOnlyTemperature()
    {
        var (client, handler) = Chat();

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { Temperature = 0.2f });

        Assert.Equal(["temperature"], Keys(handler.Body.GetProperty("options")));
    }

    [Fact]
    public async Task Chat_KeepAliveUnset_LeavesTheServerDefault()
    {
        var (client, handler) = Chat();

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.DoesNotContain("keep_alive", Keys(handler.Body));
    }

    [Fact]
    public async Task Chat_KeepAliveInAdditionalProperties_ReachesOllama()
    {
        var (client, handler) = Chat();

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions
        {
            AdditionalProperties = new() { ["keep_alive"] = "0" }
        });

        Assert.Equal("0", handler.Body.GetProperty("keep_alive").GetString());
    }

    [Fact]
    public async Task Embeddings_NoOptions_SendsNoOptionsField()
    {
        var handler = new RecordingHandler("""{"model":"e","embeddings":[[0.1]]}""");
        var client = new EmbeddingsClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions { DefaultModel = "e" }));

        await ((IEmbeddingGenerator<string, Embedding<float>>)client).GenerateAsync(["one"]);

        Assert.DoesNotContain("options", Keys(handler.Body));
    }
}
