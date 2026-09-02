using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// A tool invocation requested by the model.
/// </summary>
/// <param name="Id">Optional: Ollama's native API omits it, OpenAI-compatible servers send it.</param>
/// <param name="Type">Optional; "function" when present.</param>
/// <param name="Function">The function to invoke and its arguments.</param>
public record ToolCall(
    [property: JsonPropertyName("id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Id,
    [property: JsonPropertyName("type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Type,
    [property: JsonPropertyName("function")] FunctionCall Function
);
