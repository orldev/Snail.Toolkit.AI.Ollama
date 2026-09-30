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

    /// <summary>
    /// Replaces the system prompt baked into the model's Modelfile for this request.
    /// </summary>
    [JsonPropertyName("system")]
    public string? System { get; set; }

    /// <summary>
    /// Text that follows the insertion point, for fill-in-the-middle code completion.
    /// </summary>
    /// <remarks>Only models whose template supports infill honour it; others answer 400.</remarks>
    [JsonPropertyName("suffix")]
    public string? Suffix { get; set; }

    /// <summary>
    /// True sends the prompt as is, bypassing the model's template — the caller supplies every special token.
    /// </summary>
    [JsonPropertyName("raw")]
    public bool? Raw { get; set; }
}
