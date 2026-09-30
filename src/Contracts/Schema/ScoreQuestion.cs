using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// Places the state on an ordered scale.
/// </summary>
/// <param name="Instructions">What to judge: a string, or any object or array, sent as JSON.</param>
/// <param name="Criteria">Two to 26 descriptions from lowest to highest; the answer indexes into them.</param>
public sealed record ScoreQuestion(
    object Instructions,
    [property: JsonPropertyName("criteria")] IReadOnlyList<string> Criteria)
    : SystemOneQuestion(Instructions);
