namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// A flattened streaming chunk handed to consumers of the generate stream.
/// </summary>
public record StreamChunk(
    string Model,
    string Content,
    bool IsDone);
