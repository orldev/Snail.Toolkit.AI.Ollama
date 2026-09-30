using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// Asks how likely a yes/no statement about the state is to be true.
/// </summary>
/// <param name="Instructions">What to judge: a string, or any object or array, sent as JSON.</param>
/// <param name="Criteria">Rewords the two candidates; null keeps Ollama's "No" and "Yes".</param>
public sealed record NoulQuestion(
    object Instructions,
    [property: JsonPropertyName("criteria"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    NoulCriteria? Criteria = null)
    : SystemOneQuestion(Instructions);
