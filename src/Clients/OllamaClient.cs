using Snail.Toolkit.AI.Ollama.Abstractions;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// Facade uniting the three specialized clients behind one injectable entry point.
/// </summary>
public class OllamaClient(
    IChatClient chatClient,
    IEmbeddingsClient embeddingsClient,
    IGenerateClient generateClient)
    : IOllamaClient
{
    /// <inheritdoc />
    public IChatClient Chats { get; } = chatClient;

    /// <inheritdoc />
    public IEmbeddingsClient Embeddings { get; } = embeddingsClient;

    /// <inheritdoc />
    public IGenerateClient Generate { get; } = generateClient;
}
