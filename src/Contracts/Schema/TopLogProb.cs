using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// A candidate token with its log probability.
/// </summary>
internal record TopLogProb(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("logprob")] double Logprob,
    [property: JsonPropertyName("bytes")] IEnumerable<int>? Bytes
);
