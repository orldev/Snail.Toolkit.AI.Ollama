using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Requests;

/// <summary>
/// Shared request surface of the Ollama endpoints.
/// </summary>
public abstract record RequestBase(string Model)
{
    /// <summary>
    /// Name of the model to run, e.g. "qwen3".
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = Model;

    /// <summary>
    /// Ollama streams by default; unary calls flip this off explicitly.
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;

    /// <summary>
    /// Output format: the string "json", or a JSON Schema object for structured output.
    /// </summary>
    [JsonPropertyName("format")]
    public object? Format { get; set; }

    /// <summary>
    /// Sampling and runtime parameters overriding the model defaults.
    /// </summary>
    [JsonPropertyName("options")]
    public ModelOptions? Options { get; set; }

    /// <summary>
    /// How long the model stays loaded after the request, e.g. "5m".
    /// </summary>
    [JsonPropertyName("keep_alive")]
    public string? KeepAlive { get; set; } = "5m";

    /// <summary>
    /// Reasoning control for thinking-capable models: a boolean, or an effort level
    /// ("low", "medium", "high", "max"). Serialized as-is, so both wire shapes work.
    /// </summary>
    [JsonPropertyName("think"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Think { get; set; }
}
