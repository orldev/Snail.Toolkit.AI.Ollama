using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// How likely the statement is to be true.
/// </summary>
/// <param name="Probability">The probability of the true candidate against the false one, from 0 to 1.</param>
/// <remarks>A number rather than a boolean, so the caller decides where yes begins.</remarks>
public sealed record NoulAnswer(
    [property: JsonPropertyName("noul")] double Probability)
    : SystemOneAnswer;
