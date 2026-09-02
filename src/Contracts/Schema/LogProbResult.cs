using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// The chosen token's log probability plus the top alternatives at that position.
/// </summary>
internal record LogProbResult(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("logprob")] double Logprob,
    [property: JsonPropertyName("bytes")] IEnumerable<int>? Bytes,
    [property: JsonPropertyName("top_logprobs")] IEnumerable<TopLogProb>? TopLogprobs
);
