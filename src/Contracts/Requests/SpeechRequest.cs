using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Requests;

/// <summary>
/// The text a speech model reads aloud over /v1/audio/speech, and how.
/// </summary>
/// <param name="Model">A model with the speech capability, such as "phonon".</param>
/// <param name="Input">Up to 4,096 characters to speak.</param>
internal sealed record SpeechRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("input")] string Input)
{
    /// <summary>One of the voices /api/show lists for the model; null speaks with the model's own.</summary>
    [JsonPropertyName("voice")]
    public string? Voice { get; init; }

    /// <summary>"wav" or "pcm"; null leaves Ollama's default, wav.</summary>
    [JsonPropertyName("response_format")]
    public string? ResponseFormat { get; init; }

    /// <summary>The playback speed; null leaves the model's own.</summary>
    [JsonPropertyName("speed")]
    public float? Speed { get; init; }
}
