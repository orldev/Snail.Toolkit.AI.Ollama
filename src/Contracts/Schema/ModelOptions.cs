using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// Sampling and runtime parameters of a generation.
/// </summary>
public record ModelOptions
{
    /// <summary>
    /// Sampling randomness; higher values diversify, lower values focus.
    /// </summary>
    [JsonPropertyName("temperature")]
    public float? Temperature { get; init; }

    /// <summary>
    /// Nucleus sampling: cumulative probability mass to sample from.
    /// </summary>
    [JsonPropertyName("top_p")]
    public float? TopP { get; init; }

    /// <summary>
    /// Fixed seed for reproducible generations.
    /// </summary>
    [JsonPropertyName("seed")]
    public int? Seed { get; init; }

    /// <summary>
    /// Maximum tokens to generate — Ollama's "num_predict".
    /// </summary>
    [JsonPropertyName("num_predict")]
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Limits sampling to the K most likely tokens.
    /// </summary>
    [JsonPropertyName("top_k")]
    public int? TopK { get; init; }

    /// <summary>
    /// Sequences that stop generation when produced by the model.
    /// </summary>
    [JsonPropertyName("stop")]
    public IEnumerable<string>? Stop { get; init; }

    /// <summary>
    /// Penalizes tokens already present in the output, encouraging new topics.
    /// </summary>
    [JsonPropertyName("presence_penalty")]
    public float? PresencePenalty { get; init; }

    /// <summary>
    /// Penalizes tokens proportionally to how often they appeared, reducing repetition.
    /// </summary>
    [JsonPropertyName("frequency_penalty")]
    public float? FrequencyPenalty { get; init; }

    /// <summary>
    /// Context window size in tokens. Vision models consume it quickly — images cost
    /// hundreds of tokens each — so raise it above Ollama's small default.
    /// </summary>
    [JsonPropertyName("num_ctx")]
    public int? NumCtx { get; init; }

    /// <summary>
    /// Discards tokens whose probability relative to the best token falls below this value.
    /// </summary>
    [JsonPropertyName("min_p")]
    public float? MinP { get; init; }
}
