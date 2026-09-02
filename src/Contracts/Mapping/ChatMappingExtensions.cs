using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Snail.Toolkit.AI.Ollama.Contracts.Mapping;

/// <summary>
/// Maps between MEAI abstractions and the Ollama wire contracts.
/// </summary>
internal static class ChatMappingExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Builds the wire request. Remote image URIs raise <see cref="NotSupportedException"/> —
    /// Ollama accepts only inline base64 images.
    /// </summary>
    public static Requests.ChatRequest ToInternalRequest(
        this IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        string model)
    {
        var history = messages as IReadOnlyCollection<ChatMessage> ?? [.. messages];

        if (history.SelectMany(m => m.Contents).OfType<UriContent>().Any(u => u.HasTopLevelMediaType("image")))
        {
            throw new NotSupportedException(
                "Ollama accepts only inline images: download the image and pass it as DataContent.");
        }

        var toolNamesByCallId = MapToolNamesByCallId(history);

        return new Requests.ChatRequest(model, history.SelectMany(m => m.ToInternalMessages(toolNamesByCallId)))
        {
            Options = options?.ToInternalOptions(),
            Tools = options?.Tools?.OfType<AIFunction>().Select(f => f.ToInternalTool()),
            Think = options?.AdditionalProperties?.GetValueOrDefault("think"),
            Format = options?.ResponseFormat switch
            {
                ChatResponseFormatJson { Schema: { } schema } => schema,
                ChatResponseFormatJson => "json",
                _ => null
            }
        };
    }

    /// <summary>
    /// A tool result carries only the call id; the function name Ollama wants in tool_name
    /// comes from the matching call earlier in the conversation.
    /// </summary>
    private static Dictionary<string, string> MapToolNamesByCallId(IReadOnlyCollection<ChatMessage> history) =>
        history
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .DistinctBy(call => call.CallId)
            .ToDictionary(call => call.CallId, call => call.Name);

    /// <summary>
    /// One-to-many: Ollama wants every tool result as its own role "tool" message,
    /// while MEAI packs the results of parallel calls into a single ChatMessage.
    /// </summary>
    private static IEnumerable<Schema.ChatMessage> ToInternalMessages(
        this ChatMessage message,
        IReadOnlyDictionary<string, string> toolNamesByCallId)
    {
        List<Schema.ToolCall> toolCalls =
        [
            .. message.Contents.OfType<FunctionCallContent>().Select(call => new Schema.ToolCall(
                Id: call.CallId,
                Type: "function",
                Function: new Schema.FunctionCall(
                    Name: call.Name,
                    Arguments: JsonSerializer.SerializeToElement(call.Arguments, JsonOptions))))
        ];

        List<string> images =
        [
            .. message.Contents.OfType<DataContent>()
                .Where(data => data.HasTopLevelMediaType("image"))
                .Select(data => Convert.ToBase64String(data.Data.Span))
        ];

        var text = message.Text;
        var thinking = string.Concat(message.Contents.OfType<TextReasoningContent>().Select(r => r.Text));

        if (toolCalls.Count > 0 || text.Length > 0 || images.Count > 0)
        {
            yield return new Schema.ChatMessage(
                Role: toolCalls.Count > 0 ? "assistant" : message.Role.Value.ToLowerInvariant(),
                Content: text,
                Images: images.Count > 0 ? images : null,
                ToolCalls: toolCalls.Count > 0 ? toolCalls : null,
                Thinking: thinking.Length > 0 ? thinking : null);
        }

        foreach (var result in message.Contents.OfType<FunctionResultContent>())
        {
            yield return new Schema.ChatMessage(
                Role: "tool",
                Content: StringifyResult(result.Result),
                ToolCallId: result.CallId,
                ToolName: toolNamesByCallId.GetValueOrDefault(result.CallId));
        }
    }

    /// <summary>
    /// Strings pass through, JSON stays JSON, anything else is serialized — never Object.ToString().
    /// </summary>
    private static string StringifyResult(object? result) => result switch
    {
        null => string.Empty,
        string text => text,
        JsonElement element => element.ToString(),
        _ => JsonSerializer.Serialize(result, JsonOptions)
    };

    private static Schema.ModelOptions ToInternalOptions(this ChatOptions options) => new()
    {
        Temperature = options.Temperature,
        MaxTokens = options.MaxOutputTokens,
        TopP = options.TopP,
        TopK = options.TopK,
        Seed = (int?)options.Seed,
        Stop = options.StopSequences is { Count: > 0 } stops ? [.. stops] : null,
        PresencePenalty = options.PresencePenalty,
        FrequencyPenalty = options.FrequencyPenalty,
        NumCtx = options.AdditionalProperties?.TryGetValue("num_ctx", out int numCtx) is true ? numCtx : null,
        MinP = options.AdditionalProperties?.TryGetValue("min_p", out float minP) is true ? minP : null
    };

    private static Schema.ToolDefinition ToInternalTool(this AIFunction function) => new(
        Type: "function",
        Function: new Schema.ToolFunction(
            Name: function.Name,
            Parameters: function.JsonSchema,
            Description: function.Description is { Length: > 0 } description ? description : null));

    /// <summary>
    /// Maps a completed response; a generated call id substitutes for the one
    /// Ollama's native API never sends.
    /// </summary>
    public static ChatResponse ToAiResponse(this Responses.ChatResponse response)
    {
        var aiMessage = new ChatMessage(new ChatRole(response.Message.Role), []);

        if (!string.IsNullOrEmpty(response.Message.Thinking))
        {
            aiMessage.Contents.Add(new TextReasoningContent(response.Message.Thinking));
        }

        if (!string.IsNullOrEmpty(response.Message.Content))
        {
            aiMessage.Contents.Add(new TextContent(response.Message.Content));
        }

        MapToolCallsToContents(response.Message.ToolCalls, aiMessage.Contents);

        return new ChatResponse(aiMessage)
        {
            ModelId = response.Model,
            ResponseId = Guid.NewGuid().ToString(),
            CreatedAt = response.CreatedAt,
            FinishReason = response.ToFinishReason(),
            Usage = response.ToUsageDetails()
        };
    }

    /// <summary>
    /// Maps a streaming chunk; usage and finish reason appear only on the final one.
    /// </summary>
    public static ChatResponseUpdate ToAiUpdate(this Responses.ChatResponse response)
    {
        var update = new ChatResponseUpdate
        {
            Role = new ChatRole(response.Message.Role),
            ModelId = response.Model,
            CreatedAt = response.CreatedAt,
            RawRepresentation = response
        };

        if (!string.IsNullOrEmpty(response.Message.Thinking))
        {
            update.Contents.Add(new TextReasoningContent(response.Message.Thinking));
        }

        if (!string.IsNullOrEmpty(response.Message.Content))
        {
            update.Contents.Add(new TextContent(response.Message.Content));
        }

        MapToolCallsToContents(response.Message.ToolCalls, update.Contents);

        if (response.Done)
        {
            update.FinishReason = response.ToFinishReason();

            if (response.ToUsageDetails() is { } usage)
            {
                update.Contents.Add(new UsageContent(usage));
            }
        }

        return update;
    }

    /// <summary>
    /// ToolCalls wins over done_reason — Ollama reports "stop" even when the message
    /// requests tool execution.
    /// </summary>
    private static ChatFinishReason? ToFinishReason(this Responses.ChatResponse response) => response switch
    {
        { Done: false } => null,
        { Message.ToolCalls: { } calls } when calls.Any() => ChatFinishReason.ToolCalls,
        { DoneReason: "stop" } => ChatFinishReason.Stop,
        { DoneReason: "length" } => ChatFinishReason.Length,
        { DoneReason: { Length: > 0 } reason } => new ChatFinishReason(reason),
        _ => null
    };

    private static UsageDetails? ToUsageDetails(this Responses.ResponseBase response) =>
        response is { PromptEvalCount: null, EvalCount: null }
            ? null
            : new UsageDetails
            {
                InputTokenCount = response.PromptEvalCount,
                OutputTokenCount = response.EvalCount,
                TotalTokenCount = (response.PromptEvalCount ?? 0) + (response.EvalCount ?? 0)
            };

    private static void MapToolCallsToContents(IEnumerable<Schema.ToolCall>? internalToolCalls, IList<AIContent> targetContents)
    {
        foreach (var toolCall in internalToolCalls ?? [])
        {
            var arguments = toolCall.Function.Arguments.ValueKind is not JsonValueKind.Undefined
                ? toolCall.Function.Arguments.Deserialize<Dictionary<string, object?>>(JsonOptions)
                : null;

            targetContents.Add(new FunctionCallContent(
                callId: toolCall.Id ?? Guid.NewGuid().ToString("N"),
                name: toolCall.Function.Name,
                arguments: arguments));
        }
    }
}
