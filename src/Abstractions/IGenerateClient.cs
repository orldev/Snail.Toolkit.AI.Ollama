using Snail.Toolkit.AI.Ollama.Contracts.Requests;

namespace Snail.Toolkit.AI.Ollama.Abstractions;

/// <summary>
/// Single-shot prompt completion without conversation history.
/// </summary>
public interface IGenerateClient : IStreamingAiClient<GenerateRequest>;
