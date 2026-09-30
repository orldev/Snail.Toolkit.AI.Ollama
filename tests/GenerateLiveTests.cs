using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Live verification of /api/generate: the final chunk reports why it stopped and what it cost, and a
/// thinking model's reasoning arrives in its own field. OLLAMA_MODEL must name a thinking model.
/// </summary>
public class GenerateLiveTests
{
    private static async Task<List<StreamChunk>> StreamAsync(GenerateRequest request)
    {
        var client = new GenerateClient(new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("OLLAMA_URL")!)
        });

        List<StreamChunk> chunks = [];
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    private static string Model => Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3";

    [OllamaFact]
    public async Task StreamAsync_SystemPrompt_EndsWithReasonAndUsage()
    {
        var chunks = await StreamAsync(new GenerateRequest(Model, "Say hello.")
        {
            System = "Reply with exactly one word.",
            Think = false,
            Options = new ModelOptions { MaxTokens = 20 }
        });

        var final = chunks[^1];
        Assert.True(final.IsDone);
        Assert.True(final.DoneReason is "stop" or "length");
        Assert.True(final.Usage!.InputTokenCount > 0);
        Assert.True(final.Usage.OutputTokenCount > 0);
    }

    [OllamaFact]
    public async Task StreamAsync_ThinkingRequested_StreamsReasoningSeparately()
    {
        var chunks = await StreamAsync(new GenerateRequest(Model, "What is 2 + 2? One word.")
        {
            Think = true,
            Options = new ModelOptions { MaxTokens = 600 }
        });

        Assert.Contains(chunks, chunk => !string.IsNullOrEmpty(chunk.Thinking));
    }
}
