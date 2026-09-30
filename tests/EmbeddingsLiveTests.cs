using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Marks a fact that only runs against a live Ollama with an embedding model: OLLAMA_URL points at the
/// server and OLLAMA_EMBED_MODEL names the model, e.g. "qwen3-embedding:0.6b".
/// </summary>
public sealed class EmbeddingsFactAttribute : FactAttribute
{
    public EmbeddingsFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_URL"))
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_EMBED_MODEL")))
        {
            Skip = "Set OLLAMA_URL and OLLAMA_EMBED_MODEL to run the embeddings integration tests.";
        }
    }
}

/// <summary>
/// Live verification of the optional /api/embed fields.
/// </summary>
public class EmbeddingsLiveTests
{
    private static EmbeddingsClient CreateClient()
    {
        string baseUrl = Environment.GetEnvironmentVariable("OLLAMA_URL")!;

        return new EmbeddingsClient(
            new HttpClient { BaseAddress = new Uri(baseUrl) },
            Options.Create(new OllamaOptions
            {
                BaseUrl = baseUrl,
                DefaultModel = Environment.GetEnvironmentVariable("OLLAMA_EMBED_MODEL"),
                Timeout = TimeSpan.FromMinutes(2)
            }));
    }

    [EmbeddingsFact]
    public async Task GenerateAsync_DimensionsRequested_ShortensTheVector()
    {
        using var client = CreateClient();

        var embeddings = await ((IEmbeddingGenerator<string, Embedding<float>>)client)
            .GenerateAsync(["The cat is on the mat"], new EmbeddingGenerationOptions { Dimensions = 256 });

        Assert.Equal(256, embeddings.Single().Vector.Length);
    }

    /// <summary>
    /// With a 64-token window a few hundred words overflow; truncate=false must turn that into an error
    /// rather than a vector of the text's first lines.
    /// </summary>
    [EmbeddingsFact]
    public async Task GenerateAsync_TruncateOffAndInputTooLong_Fails()
    {
        using var client = CreateClient();
        string longText = string.Join(' ', Enumerable.Repeat("snails travel slowly across the continent", 100));

        var request = new EmbeddingsRequest(Environment.GetEnvironmentVariable("OLLAMA_EMBED_MODEL")!, [longText],
            new Dictionary<string, int> { ["num_ctx"] = 64 })
        {
            Truncate = false
        };

        await Assert.ThrowsAsync<HttpBuilderException>(() => client.GenerateAsync(request));
    }
}
