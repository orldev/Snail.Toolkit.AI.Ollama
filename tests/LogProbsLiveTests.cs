using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using LogProbResult = Snail.Toolkit.AI.Ollama.Contracts.Schema.LogProbResult;
using ModelOptions = Snail.Toolkit.AI.Ollama.Contracts.Schema.ModelOptions;
using StreamChunk = Snail.Toolkit.AI.Ollama.Contracts.Schema.StreamChunk;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Live verification that token log probabilities come back from both chat and generate.
/// </summary>
public class LogProbsLiveTests
{
    private static string BaseUrl => Environment.GetEnvironmentVariable("OLLAMA_URL")!;

    private static string Model => Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3";

    [OllamaFact]
    public async Task GetResponseAsync_TopLogProbsRequested_ReturnsAlternativesPerToken()
    {
        var client = new ChatClient(
            new HttpClient { BaseAddress = new Uri(BaseUrl) },
            Options.Create(new OllamaOptions { BaseUrl = BaseUrl, DefaultModel = Model, Timeout = TimeSpan.FromMinutes(5) }));

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Say hi.")], new ChatOptions
        {
            MaxOutputTokens = 5,
            AdditionalProperties = new() { ["top_logprobs"] = 2, ["think"] = false }
        });

        var logProbs = Assert.IsAssignableFrom<IReadOnlyList<LogProbResult>>(response.AdditionalProperties!["logprobs"]);
        Assert.NotEmpty(logProbs);
        Assert.All(logProbs, token => Assert.Equal(2, token.TopLogprobs!.Count));
    }

    [OllamaFact]
    public async Task StreamAsync_LogProbsRequested_EveryTextChunkCarriesThem()
    {
        var client = new GenerateClient(new HttpClient { BaseAddress = new Uri(BaseUrl) });

        List<StreamChunk> chunks = [];
        await foreach (var chunk in client.StreamAsync(new GenerateRequest(Model, "Say hi.")
                       {
                           LogProbs = true,
                           Think = false,
                           Options = new ModelOptions { MaxTokens = 5 }
                       }))
        {
            chunks.Add(chunk);
        }

        Assert.All(chunks.Where(chunk => chunk.Content.Length > 0), chunk => Assert.NotNull(chunk.LogProbs));
    }
}
