using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// The assistant message inside a chat response, including reasoning and tool calls.
/// </summary>
internal record Message(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("thinking")] string? Thinking,
    [property: JsonPropertyName("images")] IEnumerable<string>? Images,
    [property: JsonPropertyName("tool_calls")] IEnumerable<ToolCall>? ToolCalls
);
