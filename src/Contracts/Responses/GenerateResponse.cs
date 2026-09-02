using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// One /api/generate payload — a full response or a single streaming chunk.
/// </summary>
internal record GenerateResponse : ResponseBase
{
    [JsonPropertyName("response")]
    public string Response { get; init; } = string.Empty;

    [JsonPropertyName("thinking")]
    public string? Thinking { get; init; }
}
