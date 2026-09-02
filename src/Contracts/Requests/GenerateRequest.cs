using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Requests;

/// <summary>
/// A single-shot completion request for /api/generate.
/// </summary>
public record GenerateRequest(string Model, string Prompt) : RequestBase(Model)
{
    /// <summary>
    /// The text to complete.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; } = Prompt;

    /// <summary>
    /// Base64-encoded images for multimodal models.
    /// </summary>
    [JsonPropertyName("images")]
    public IEnumerable<string>? Images { get; set; }
}
