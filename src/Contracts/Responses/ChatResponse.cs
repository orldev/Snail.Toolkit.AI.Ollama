using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// One /api/chat payload — a full response or a single streaming chunk.
/// </summary>
internal record ChatResponse : ResponseBase
{
    /// <remarks>
    /// Not required: a mid-stream {"error": ...} line carries no message, and failing its deserialization
    /// would hide Ollama's own explanation behind a missing-property error.
    /// </remarks>
    [JsonPropertyName("message")]
    public Message Message { get; init; } = new("assistant", string.Empty, null, null, null);
}
