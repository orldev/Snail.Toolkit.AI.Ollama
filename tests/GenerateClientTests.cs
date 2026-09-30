using System.Net;
using System.Text;
using System.Text.Json;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Tests for <see cref="GenerateClient"/>: the request fields it sends and what each chunk carries back.
/// </summary>
public class GenerateClientTests
{
    private const string ThinkingStream = """
        {"model":"m","response":"","thinking":"Two plus two","done":false}
        {"model":"m","response":"Four","done":false}
        {"model":"m","response":"","done":true,"done_reason":"stop","prompt_eval_count":12,"prompt_eval_cached_count":4,"eval_count":9}
        """;

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public JsonElement Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/x-ndjson")
            };
        }
    }

    private static async Task<(List<StreamChunk> Chunks, JsonElement Body)> StreamAsync(GenerateRequest request)
    {
        var handler = new StubHandler(ThinkingStream);
        var client = new GenerateClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") });

        List<StreamChunk> chunks = [];
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        return (chunks, handler.Body);
    }

    [Fact]
    public async Task StreamAsync_SystemSuffixAndRaw_ReachOllama()
    {
        var (_, body) = await StreamAsync(new GenerateRequest("m", "def add(a, b):")
        {
            System = "You write Python.",
            Suffix = "    return result",
            Raw = true
        });

        Assert.Equal("You write Python.", body.GetProperty("system").GetString());
        Assert.Equal("    return result", body.GetProperty("suffix").GetString());
        Assert.True(body.GetProperty("raw").GetBoolean());
    }

    [Fact]
    public async Task StreamAsync_PlainPrompt_SendsNoOptionalFields()
    {
        var (_, body) = await StreamAsync(new GenerateRequest("m", "hi"));

        var keys = body.EnumerateObject().Select(property => property.Name).ToList();
        Assert.DoesNotContain("system", keys);
        Assert.DoesNotContain("suffix", keys);
        Assert.DoesNotContain("raw", keys);
    }

    [Fact]
    public async Task StreamAsync_ThinkingChunk_CarriesReasoning()
    {
        var (chunks, _) = await StreamAsync(new GenerateRequest("m", "2+2?"));

        Assert.Equal("Two plus two", chunks[0].Thinking);
        Assert.Equal(string.Empty, chunks[0].Content);
        Assert.Null(chunks[1].Thinking);
    }

    [Fact]
    public async Task StreamAsync_FinalChunk_CarriesDoneReasonAndUsage()
    {
        var (chunks, _) = await StreamAsync(new GenerateRequest("m", "2+2?"));

        var final = chunks[^1];
        Assert.True(final.IsDone);
        Assert.Equal("stop", final.DoneReason);
        Assert.Equal(12, final.Usage!.InputTokenCount);
        Assert.Equal(4, final.Usage.CachedInputTokenCount);
        Assert.Equal(9, final.Usage.OutputTokenCount);
        Assert.All(chunks[..^1], chunk => Assert.Null(chunk.Usage));
    }
}
