using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// The /v1/systemone payload: one answer per question, under the name it was asked with.
/// </summary>
/// <param name="Model">The model named in the request.</param>
/// <param name="Answers">Answers keyed by question name.</param>
/// <param name="Usage">Tokens the call consumed.</param>
public sealed record SystemOneResponse(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("answers")] IReadOnlyDictionary<string, SystemOneAnswer> Answers,
    [property: JsonPropertyName("usage")] SystemOneUsage Usage);
