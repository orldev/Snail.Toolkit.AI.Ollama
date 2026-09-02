using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Requests;

/// <summary>
/// A batch vectorization request for /api/embed.
/// </summary>
public record EmbeddingsRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input")] IEnumerable<string> Input,
    [property: JsonPropertyName("options")] object? Options = null);
