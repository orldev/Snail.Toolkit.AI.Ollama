using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Requests;

/// <summary>
/// A batch vectorization request for /api/embed.
/// </summary>
public record EmbeddingsRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input")] IEnumerable<string> Input,
    [property: JsonPropertyName("options")] object? Options = null)
{
    /// <summary>
    /// Shortens every vector to this many dimensions, for models trained to allow it.
    /// </summary>
    [JsonPropertyName("dimensions")]
    public int? Dimensions { get; init; }

    /// <summary>
    /// False makes an input longer than the context window fail instead of being cut; null keeps Ollama's
    /// default, which truncates.
    /// </summary>
    /// <remarks>
    /// A truncated input still returns a vector, just one for the text's beginning — search quietly degrades
    /// with nothing in the response to say why.
    /// </remarks>
    [JsonPropertyName("truncate")]
    public bool? Truncate { get; init; }

    /// <summary>
    /// How long the model stays loaded after the request; null leaves the server's OLLAMA_KEEP_ALIVE.
    /// </summary>
    [JsonPropertyName("keep_alive")]
    public string? KeepAlive { get; init; }
}
