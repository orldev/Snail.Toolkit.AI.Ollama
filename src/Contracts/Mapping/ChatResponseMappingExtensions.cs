using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Snail.Toolkit.AI.Ollama.Contracts.Mapping;

/// <summary>
/// Maps Ollama's /api/chat answers and stream chunks to MEAI responses and updates.
/// </summary>
internal static class ChatResponseMappingExtensions
{
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
            Usage = response.ToUsageDetails(),
            AdditionalProperties = response.ToAdditionalProperties()
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
            RawRepresentation = response,
            AdditionalProperties = response.ToAdditionalProperties()
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

        if (response.IsDone)
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
    /// Token counters of a final chunk as MEAI usage; null when Ollama sent none.
    /// </summary>
    public static UsageDetails? ToUsageDetails(this Responses.ResponseBase response) =>
        response is { PromptEvalCount: null, EvalCount: null }
            ? null
            : new UsageDetails
            {
                InputTokenCount = response.PromptEvalCount,
                CachedInputTokenCount = response.PromptEvalCachedCount,
                OutputTokenCount = response.EvalCount,
                TotalTokenCount = (response.PromptEvalCount ?? 0) + (response.EvalCount ?? 0)
            };

    /// <summary>
    /// Log probabilities ride in AdditionalProperties["logprobs"]: MEAI has no typed slot for them.
    /// </summary>
    private static AdditionalPropertiesDictionary? ToAdditionalProperties(this Responses.ResponseBase response) =>
        response.LogProbs is { Count: > 0 } logProbs
            ? new AdditionalPropertiesDictionary { ["logprobs"] = logProbs }
            : null;

    /// <summary>
    /// ToolCalls wins over done_reason — Ollama reports "stop" even when the message
    /// requests tool execution.
    /// </summary>
    private static ChatFinishReason? ToFinishReason(this Responses.ChatResponse response) => response switch
    {
        { IsDone: false } => null,
        { Message.ToolCalls: { } calls } when calls.Any() => ChatFinishReason.ToolCalls,
        { DoneReason: "stop" } => ChatFinishReason.Stop,
        { DoneReason: "length" } => ChatFinishReason.Length,
        { DoneReason: { Length: > 0 } reason } => new ChatFinishReason(reason),
        _ => null
    };

    private static void MapToolCallsToContents(IEnumerable<Schema.ToolCall>? internalToolCalls, IList<AIContent> targetContents)
    {
        foreach (var toolCall in internalToolCalls ?? [])
        {
            var arguments = toolCall.Function.Arguments.ValueKind is not JsonValueKind.Undefined
                ? toolCall.Function.Arguments.Deserialize(ToolJson.ReadArguments)
                : null;

            targetContents.Add(new FunctionCallContent(
                callId: toolCall.Id ?? Guid.NewGuid().ToString("N"),
                name: toolCall.Function.Name,
                arguments: arguments));
        }
    }
}
