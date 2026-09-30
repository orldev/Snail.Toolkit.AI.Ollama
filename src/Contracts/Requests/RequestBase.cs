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
    /// How long the model stays loaded after the request: "10m", "0" to unload at once, "-1m" to keep it.
    /// </summary>
    /// <remarks>
    /// Null leaves the server's OLLAMA_KEEP_ALIVE in charge; a hardcoded default here overrode whatever
    /// the operator had configured on every single request.
    /// </remarks>
    [JsonPropertyName("keep_alive")]
    public string? KeepAlive { get; set; }

    /// <summary>
    /// Reasoning control for thinking-capable models: a boolean, or an effort level
    /// ("low", "medium", "high", "max"). Serialized as-is, so both wire shapes work.
    /// </summary>
    [JsonPropertyName("think"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Think { get; set; }

    /// <summary>
    /// True returns the log probability of every generated token.
    /// </summary>
    [JsonPropertyName("logprobs")]
    public bool? LogProbs { get; set; }

    /// <summary>
    /// How many most-likely alternatives to report per token, from 0 to 20; needs <see cref="LogProbs"/>.
    /// </summary>
    /// <remarks>Ollama answers 400 above 20.</remarks>
    [JsonPropertyName("top_logprobs")]
    public int? TopLogProbs { get; set; }
}
