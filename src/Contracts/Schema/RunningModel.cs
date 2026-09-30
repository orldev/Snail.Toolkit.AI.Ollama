using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// A model loaded in memory right now, as /api/ps lists it.
/// </summary>
/// <param name="Name">The name it was loaded by.</param>
/// <param name="Model">The model reference.</param>
/// <param name="Size">Memory it occupies in bytes.</param>
/// <param name="Digest">The SHA-256 digest of its manifest.</param>
/// <param name="Details">Format, family, size and quantization.</param>
/// <param name="ExpiresAt">When keep_alive runs out and the model unloads.</param>
/// <param name="SizeVram">The part of <paramref name="Size"/> held in GPU memory.</param>
/// <param name="ContextLength">The context window it was loaded with.</param>
/// <remarks>A <paramref name="SizeVram"/> below <paramref name="Size"/> means layers spilled to the CPU, which is slow.</remarks>
public sealed record RunningModel(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("details")] ModelDetails? Details,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("size_vram")] long SizeVram,
    [property: JsonPropertyName("context_length")] int? ContextLength);
