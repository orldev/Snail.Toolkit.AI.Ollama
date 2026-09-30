using System.Text.Json;
using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// Everything /api/show knows about one model: what it can do, how it is built and how it is prompted.
/// </summary>
/// <param name="Capabilities">What it can do — "completion", "tools", "thinking", "vision", "embedding", "decision".</param>
/// <param name="Details">Format, family, size and quantization.</param>
/// <param name="ModifiedAt">When it was last pulled or created.</param>
/// <param name="Parameters">The Modelfile parameters, one "name value" per line.</param>
/// <param name="License">The license text shipped with the weights.</param>
/// <param name="Template">The prompt template.</param>
/// <param name="ModelInfo">Architecture metadata keyed like "gemma3.context_length".</param>
/// <param name="Thinking">The think values the model accepts, for thinking models that declare them.</param>
public sealed record ModelDescription(
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string>? Capabilities,
    [property: JsonPropertyName("details")] ModelDetails? Details,
    [property: JsonPropertyName("modified_at")] DateTimeOffset? ModifiedAt,
    [property: JsonPropertyName("parameters")] string? Parameters,
    [property: JsonPropertyName("license")] string? License,
    [property: JsonPropertyName("template")] string? Template,
    [property: JsonPropertyName("model_info")] IReadOnlyDictionary<string, JsonElement>? ModelInfo,
    [property: JsonPropertyName("thinking")] ThinkingLevels? Thinking = null)
{
    /// <summary>
    /// Whether the model declares the capability, e.g. Supports("tools") before offering it tools.
    /// </summary>
    /// <remarks>
    /// Asking first is cheaper than the 400 Ollama answers when a model lacks it — a thinking request to a
    /// model without "thinking", a System One call to one without "decision".
    /// </remarks>
    public bool Supports(string capability) =>
        Capabilities?.Contains(capability, StringComparer.OrdinalIgnoreCase) is true;

    /// <summary>
    /// The trained context window in tokens, read from the architecture metadata; null when not reported.
    /// </summary>
    public int? ContextLength
    {
        get
        {
            var entry = ModelInfo?.FirstOrDefault(pair => pair.Key.EndsWith(".context_length", StringComparison.Ordinal));

            return entry?.Value is { ValueKind: JsonValueKind.Number } length && length.TryGetInt32(out int tokens)
                ? tokens
                : null;
        }
    }
}

/// <summary>
/// The think values a model accepts and the one it uses when none is sent.
/// </summary>
/// <param name="Values">Booleans and effort names, e.g. [false, true] or ["low", "medium", "high"].</param>
/// <param name="Default">The value used when a request sends no think.</param>
public sealed record ThinkingLevels(
    [property: JsonPropertyName("values")] IReadOnlyList<JsonElement> Values,
    [property: JsonPropertyName("default")] JsonElement? Default);
