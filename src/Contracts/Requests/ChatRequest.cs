using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Requests;

/// <summary>
/// A multi-turn conversation request for /api/chat.
/// </summary>
public record ChatRequest(string Model, IEnumerable<ChatMessage> Messages) : RequestBase(Model)
{
    /// <summary>
    /// The conversation history, oldest first.
    /// </summary>
    [JsonPropertyName("messages")]
    public IEnumerable<ChatMessage>? Messages { get; set; } = Messages;

    /// <summary>
    /// Functions the model may call.
    /// </summary>
    [JsonPropertyName("tools")]
    public IEnumerable<ToolDefinition>? Tools { get; set; }
}
