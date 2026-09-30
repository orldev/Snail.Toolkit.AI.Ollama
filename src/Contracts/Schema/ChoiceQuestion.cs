using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// Picks the one option that fits the state best.
/// </summary>
/// <param name="Instructions">What to judge: a string, or any object or array, sent as JSON.</param>
/// <param name="Criteria">
/// Two to 26 options keyed by the name the answer returns; a null description lets the key speak for itself.
/// </param>
/// <remarks>A tie goes to the option listed first, so order the likelier default first.</remarks>
public sealed record ChoiceQuestion(
    object Instructions,
    [property: JsonPropertyName("criteria")] IReadOnlyDictionary<string, string?> Criteria)
    : SystemOneQuestion(Instructions);
