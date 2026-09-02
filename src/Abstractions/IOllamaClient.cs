namespace Snail.Toolkit.AI.Ollama.Abstractions;

/// <summary>
/// Unified entry point: chat, embeddings and single-shot generation behind one dependency.
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    /// Multi-turn conversations with tool calling, thinking and images — the MEAI-compatible surface.
    /// </summary>
    IChatClient Chats { get; }

    /// <summary>
    /// Text vectorization for semantic search and retrieval scenarios.
    /// </summary>
    IEmbeddingsClient Embeddings { get; }

    /// <summary>
    /// One-off prompt completion without conversation history.
    /// </summary>
    IGenerateClient Generate { get; }
}
