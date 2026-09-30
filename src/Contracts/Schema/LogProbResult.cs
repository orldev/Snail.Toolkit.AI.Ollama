using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// The chosen token's log probability plus the top alternatives at that position.
/// </summary>
/// <param name="Token">The token the model emitted.</param>
/// <param name="Logprob">Its natural-log probability; 0 is certainty, more negative is less likely.</param>
/// <param name="Bytes">The token's UTF-8 bytes, for tokens that split a multi-byte character.</param>
/// <param name="TopLogprobs">The most likely candidates at this position, when top_logprobs was requested.</param>
public sealed record LogProbResult(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("logprob")] double Logprob,
    [property: JsonPropertyName("bytes")] IReadOnlyList<int>? Bytes,
    [property: JsonPropertyName("top_logprobs")] IReadOnlyList<TopLogProb>? TopLogprobs
);
