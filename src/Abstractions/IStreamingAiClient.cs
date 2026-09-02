using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Abstractions;

/// <summary>
/// A client whose response arrives as a real-time stream of chunks.
/// </summary>
/// <typeparam name="TRequest">The wire request this client understands.</typeparam>
public interface IStreamingAiClient<in TRequest>
{
    /// <summary>
    /// Streams the model output chunk by chunk; the stream ends with the final chunk
    /// or when the caller's token fires.
    /// </summary>
    IAsyncEnumerable<StreamChunk> StreamAsync(TRequest request, CancellationToken cancellationToken = default);
}
