using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// The function half of a tool call: name plus raw JSON arguments.
/// </summary>
public record FunctionCall(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] JsonElement Arguments,
    [property: JsonPropertyName("description")] string? Description = null);
