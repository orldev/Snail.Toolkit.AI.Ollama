using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// The /api/embed payload: one vector per input string, in input order.
/// </summary>
public record EmbeddingsResponse(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("embeddings")] float[][] Embeddings,
    [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount);
