using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// A tool offered to the model in a chat request.
/// </summary>
public record ToolDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] ToolFunction Function
);

/// <summary>
/// A callable function: name, JSON Schema of parameters, and an optional description
/// that guides the model's choice.
/// </summary>
public record ToolFunction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parameters")] JsonElement Parameters,
    [property: JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description = null
);
