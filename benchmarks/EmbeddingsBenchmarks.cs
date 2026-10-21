using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.AI;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Benchmarks;

/// <summary>
/// Sixteen texts embedded in 768 dimensions: about 190 KB of JSON parsed into float vectors.
/// </summary>
[MemoryDiagnoser]
public class EmbeddingsBenchmarks
{
    private static readonly EmbeddingGenerationOptions Options = new() { ModelId = "nomic-embed-text" };

    private readonly string[] _inputs = [.. Enumerable.Range(0, 16).Select(index => $"Document number {index} talks about distributed schedulers and object storage.")];

    private EmbeddingsClient _client = null!;

    [GlobalSetup]
    public void Setup()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OllamaOptions());

        _client = new EmbeddingsClient(CannedOllama.Answering(CannedOllama.Embeddings(16, 768)), options);
    }

    [Benchmark]
    public Task<GeneratedEmbeddings<Embedding<float>>> Generate() => _client.GenerateAsync(_inputs, Options);
}
