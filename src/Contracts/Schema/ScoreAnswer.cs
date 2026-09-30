using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// Where the state lands on the question's scale.
/// </summary>
/// <param name="Score">The probability-weighted mean of the criterion indices, from 0 to N − 1.</param>
/// <param name="Legend">The criterion description behind every index.</param>
/// <param name="Probabilities">The probability of every index.</param>
/// <param name="Confidence">How concentrated the distribution is: 0 when uniform, near 1 when one index dominates.</param>
/// <remarks>
/// The score is not normalized to 0–1: on a three-step scale 1.5 sits halfway between the middle and the
/// top description, so divide by N − 1 to compare scales of different length.
/// </remarks>
public sealed record ScoreAnswer(
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("legend")] IReadOnlyDictionary<int, string> Legend,
    [property: JsonPropertyName("probabilities")] IReadOnlyDictionary<int, double> Probabilities,
    [property: JsonPropertyName("confidence")] double Confidence)
    : SystemOneAnswer;
