using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// What a model is built from: format, family, size and quantization.
/// </summary>
/// <param name="ParentModel">The model or weights file it was derived from, when known.</param>
/// <param name="Format">The weights format, e.g. "gguf".</param>
/// <param name="Family">The architecture family, e.g. "gemma3".</param>
/// <param name="Families">Every family the architecture belongs to.</param>
/// <param name="ParameterSize">The parameter count as Ollama prints it, e.g. "999.89M".</param>
/// <param name="QuantizationLevel">The quantization, e.g. "Q4_K_M".</param>
/// <param name="ContextLength">The trained context window in tokens, when the listing reports it.</param>
/// <param name="EmbeddingLength">The width of the model's hidden state, when the listing reports it.</param>
public sealed record ModelDetails(
    [property: JsonPropertyName("parent_model")] string? ParentModel,
    [property: JsonPropertyName("format")] string? Format,
    [property: JsonPropertyName("family")] string? Family,
    [property: JsonPropertyName("families")] IReadOnlyList<string>? Families,
    [property: JsonPropertyName("parameter_size")] string? ParameterSize,
    [property: JsonPropertyName("quantization_level")] string? QuantizationLevel,
    [property: JsonPropertyName("context_length")] int? ContextLength = null,
    [property: JsonPropertyName("embedding_length")] int? EmbeddingLength = null);
