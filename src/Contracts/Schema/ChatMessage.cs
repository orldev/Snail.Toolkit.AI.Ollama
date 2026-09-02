using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// A wire-level conversation message.
/// </summary>
/// <param name="Role">"system", "user", "assistant" or "tool".</param>
/// <param name="Content">The message text.</param>
/// <param name="Images">Base64-encoded images; Ollama accepts them on user messages only.</param>
/// <param name="ToolCalls">Tool invocations requested by the assistant.</param>
/// <param name="ToolCallId">Links a tool result to its call — the OpenAI-compatible convention.</param>
/// <param name="ToolName">Attributes a tool result by function name — Ollama's native convention.</param>
/// <param name="Thinking">Assistant reasoning replayed to thinking-capable models.</param>
public record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("images")] IEnumerable<string>? Images = null,
    [property: JsonPropertyName("tool_calls")] IEnumerable<ToolCall>? ToolCalls = null,
    [property: JsonPropertyName("tool_call_id")] string? ToolCallId = null,
    [property: JsonPropertyName("tool_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ToolName = null,
    [property: JsonPropertyName("thinking"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Thinking = null
);
