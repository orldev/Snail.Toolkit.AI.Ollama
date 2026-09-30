using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// A candidate token with its log probability.
/// </summary>
/// <param name="Token">The candidate token.</param>
/// <param name="Logprob">Its natural-log probability.</param>
/// <param name="Bytes">The token's UTF-8 bytes.</param>
public sealed record TopLogProb(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("logprob")] double Logprob,
    [property: JsonPropertyName("bytes")] IReadOnlyList<int>? Bytes
);
