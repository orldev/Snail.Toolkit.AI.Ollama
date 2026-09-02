using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// One /api/chat payload — a full response or a single streaming chunk.
/// </summary>
internal record ChatResponse : ResponseBase
{
    [JsonPropertyName("message")]
    public required Message Message { get; init; }
}
