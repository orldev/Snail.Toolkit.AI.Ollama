using Snail.Toolkit.AI.Ollama.Abstractions;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// Facade uniting the specialized clients behind one injectable entry point.
/// </summary>
public class OllamaClient(
    IChatClient chatClient,
    IEmbeddingsClient embeddingsClient,
    IGenerateClient generateClient,
    ISystemOneClient systemOneClient,
    IModelsClient modelsClient)
    : IOllamaClient
{
    /// <inheritdoc />
    public IChatClient Chats { get; } = chatClient;

    /// <inheritdoc />
    public IEmbeddingsClient Embeddings { get; } = embeddingsClient;

    /// <inheritdoc />
    public IGenerateClient Generate { get; } = generateClient;

    /// <inheritdoc />
    public ISystemOneClient SystemOne { get; } = systemOneClient;

    /// <inheritdoc />
    public IModelsClient Models { get; } = modelsClient;
}
