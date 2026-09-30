using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// The option the model picked, with the distribution it picked from.
/// </summary>
/// <param name="Choice">The key of the most probable criterion.</param>
/// <param name="Probabilities">The probability of every criterion, keyed as in the question.</param>
/// <param name="Confidence">How concentrated the distribution is: 0 when uniform, near 1 when one option dominates.</param>
/// <remarks>Confidence is 1 − H(p) / ln(N), so it stays comparable between questions with different option counts.</remarks>
public sealed record ChoiceAnswer(
    [property: JsonPropertyName("choice")] string Choice,
    [property: JsonPropertyName("probabilities")] IReadOnlyDictionary<string, double> Probabilities,
    [property: JsonPropertyName("confidence")] double Confidence)
    : SystemOneAnswer;
