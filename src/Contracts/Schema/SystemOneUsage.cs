using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// Tokens a System One call consumed.
/// </summary>
/// <param name="InputTokens">The rendered prompts of all questions added up.</param>
/// <param name="OutputTokens">Tokens generated internally to score, including preparation and retries.</param>
/// <remarks>
/// The state is counted once per question, even when Ollama served it from cache, so input tokens grow with
/// the question count rather than with the work actually done.
/// </remarks>
public sealed record SystemOneUsage(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens);
