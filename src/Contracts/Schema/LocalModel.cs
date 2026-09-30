using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// A model pulled to the server's disk, as /api/tags lists it.
/// </summary>
/// <param name="Name">The name to call it by, e.g. "gemma3:1b".</param>
/// <param name="Model">The model reference, usually the same as the name.</param>
/// <param name="ModifiedAt">When it was last pulled or created.</param>
/// <param name="Size">Its size on disk in bytes.</param>
/// <param name="Digest">The SHA-256 digest of its manifest.</param>
/// <param name="Details">Format, family, size and quantization.</param>
/// <param name="Capabilities">What it can do — "completion", "tools", "thinking", "vision", "decision".</param>
public sealed record LocalModel(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("modified_at")] DateTimeOffset ModifiedAt,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("details")] ModelDetails? Details,
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string>? Capabilities = null);
