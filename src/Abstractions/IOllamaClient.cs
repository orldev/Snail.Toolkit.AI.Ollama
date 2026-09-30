namespace Snail.Toolkit.AI.Ollama.Abstractions;

/// <summary>
/// Unified entry point: chat, embeddings, single-shot generation, System One scoring and model queries
/// behind one dependency.
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

    /// <summary>
    /// Choice, yes/no and score questions answered as probabilities by a local System One model.
    /// </summary>
    ISystemOneClient SystemOne { get; }

    /// <summary>
    /// The server's version and its models: on disk, in memory, and what each can do.
    /// </summary>
    IModelsClient Models { get; }
}
