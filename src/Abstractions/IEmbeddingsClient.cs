using Microsoft.Extensions.AI;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;

namespace Snail.Toolkit.AI.Ollama.Abstractions;

/// <summary>
/// Text vectorization; also a MEAI <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>,
/// so it plugs into MEAI pipelines the same way the chat client does.
/// </summary>
public interface IEmbeddingsClient : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>
    /// Raw wire-level call for batch inputs; unlike chat, embeddings never stream.
    /// </summary>
    Task<EmbeddingsResponse> GenerateAsync(EmbeddingsRequest request, CancellationToken cancellationToken = default);
}
