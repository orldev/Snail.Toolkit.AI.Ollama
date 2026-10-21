using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Snail.Toolkit.AI.Ollama.Contracts.Mapping;

/// <summary>
/// Maps between MEAI abstractions and the Ollama wire contracts.
/// </summary>
internal static class ChatMappingExtensions
{
    /// <summary>
    /// The options tool-call arguments and results are written and read with: names matched in any case,
    /// metadata from <see cref="MappingJson"/> first and from MEAI's resolver for anything else.
    /// </summary>
    /// <remarks>
    /// MEAI's resolver adds reflection only where the application allows it, so a tool result of any
    /// type serializes under the JIT as before; under Native AOT it has to be a type MEAI knows.
    /// </remarks>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(MappingJson.Default, AIJsonUtilities.DefaultOptions.TypeInfoResolver)
    };

    private static readonly JsonTypeInfo<IDictionary<string, object?>?> WrittenArguments =
        (JsonTypeInfo<IDictionary<string, object?>?>)JsonOptions.GetTypeInfo(typeof(IDictionary<string, object?>));

    private static readonly JsonTypeInfo<object> WrittenResult =
        (JsonTypeInfo<object>)JsonOptions.GetTypeInfo(typeof(object));

    private static readonly JsonTypeInfo<Dictionary<string, object?>> ReadArguments =
        (JsonTypeInfo<Dictionary<string, object?>>)JsonOptions.GetTypeInfo(typeof(Dictionary<string, object?>));

    /// <summary>
    /// Builds the wire request. An attachment Ollama cannot read raises <see cref="NotSupportedException"/>.
    /// </summary>
    public static Requests.ChatRequest ToInternalRequest(
        this IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        string model)
    {
        var history = messages as IReadOnlyCollection<ChatMessage> ?? [.. messages];

        RefuseUnreadableAttachments(history);

        var toolNamesByCallId = MapToolNamesByCallId(history);
        var conversation = history.SelectMany(m => m.ToInternalMessages(toolNamesByCallId));

        var request = new Requests.ChatRequest(model, [.. options.ToInstructionMessages(), .. conversation])
        {
            Options = options?.ToInternalOptions(),
            Tools = options?.ToolMode is NoneChatToolMode
                ? null
                : options?.Tools?.OfType<AIFunctionDeclaration>().Select(f => f.ToInternalTool()),
            Think = options.ToThink(),
            KeepAlive = options?.AdditionalProperties?.TryGetValue("keep_alive", out string? keepAlive) is true
                ? keepAlive
                : null,
            TopLogProbs = options.ToTopLogProbs(),
            LogProbs = options?.AdditionalProperties?.TryGetValue("logprobs", out bool logProbs) is true
                ? logProbs
                : options.ToTopLogProbs() is not null ? true : null,
            Format = options?.ResponseFormat switch
            {
                ChatResponseFormatJson { Schema: { } schema } => schema,
                ChatResponseFormatJson => "json",
                _ => null
            }
        };

        return request;
    }

    /// <summary>
    /// Ollama reads text and inline base64 images only; any other attachment would never reach the model.
    /// </summary>
    /// <remarks>
    /// A PDF or audio DataContent, and any remote URI, used to be dropped without a word, and the model answered
    /// as if nothing had been attached.
    /// </remarks>
    /// <exception cref="NotSupportedException">An attachment is remote, or inline but not an image.</exception>
    private static void RefuseUnreadableAttachments(IReadOnlyCollection<ChatMessage> history)
    {
        foreach (var content in history.SelectMany(message => message.Contents))
        {
            if (content is UriContent remote)
            {
                throw new NotSupportedException(remote.HasTopLevelMediaType("image")
                    ? "Ollama accepts only inline images: download the image and pass it as DataContent."
                    : $"Ollama cannot read a remote {remote.MediaType} attachment: only text and inline images reach the model.");
            }

            if (content is DataContent data && !data.HasTopLevelMediaType("image"))
            {
                throw new NotSupportedException(
                    $"Ollama cannot read a {data.MediaType} attachment: only text and inline images reach the model; extract its text first.");
            }
        }
    }

    /// <summary>
    /// Per-request instructions travel as a leading system message: Ollama has no field of their own.
    /// </summary>
    private static IEnumerable<Schema.ChatMessage> ToInstructionMessages(this ChatOptions? options) =>
        options?.Instructions is { Length: > 0 } instructions
            ? [new Schema.ChatMessage(Role: "system", Content: instructions)]
            : [];

    /// <summary>
    /// An explicit AdditionalProperties["think"] wins; otherwise the MEAI reasoning effort picks the level.
    /// </summary>
    /// <remarks>
    /// Measured on Ollama 0.35.0: a model whose /api/show lists only [false, true] still accepts "low",
    /// "medium" and "max" and simply thinks, while a model without the thinking capability rejects any
    /// think value with 400 — so nothing is sent unless the caller asked for reasoning.
    /// </remarks>
    private static object? ToThink(this ChatOptions? options) =>
        options?.AdditionalProperties?.GetValueOrDefault("think")
        ?? options?.Reasoning?.Effort switch
        {
            ReasoningEffort.None => false,
            ReasoningEffort.Low => "low",
            ReasoningEffort.Medium => "medium",
            ReasoningEffort.High => "high",
            ReasoningEffort.ExtraHigh => "max",
            _ => null
        };

    /// <summary>
    /// Asking for alternatives implies asking for log probabilities, which Ollama otherwise leaves off.
    /// </summary>
    private static int? ToTopLogProbs(this ChatOptions? options) =>
        options?.AdditionalProperties?.TryGetValue("top_logprobs", out int top) is true ? top : null;

    /// <summary>
    /// Log probabilities ride in AdditionalProperties["logprobs"]: MEAI has no typed slot for them.
    /// </summary>
    private static AdditionalPropertiesDictionary? ToAdditionalProperties(this Responses.ResponseBase response) =>
        response.LogProbs is { Count: > 0 } logProbs
            ? new AdditionalPropertiesDictionary { ["logprobs"] = logProbs }
            : null;

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
                    Arguments: JsonSerializer.SerializeToElement(call.Arguments, WrittenArguments))))
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
        _ => JsonSerializer.Serialize(result, WrittenResult)
    };

    private static Schema.ModelOptions ToInternalOptions(this ChatOptions options) => new()
    {
        Temperature = options.Temperature,
        MaxTokens = options.MaxOutputTokens,
        TopP = options.TopP,
        TopK = options.TopK,
        Seed = options.Seed,
        Stop = options.StopSequences is { Count: > 0 } stops ? [.. stops] : null,
        PresencePenalty = options.PresencePenalty,
        FrequencyPenalty = options.FrequencyPenalty,
        NumCtx = options.AdditionalProperties?.TryGetValue("num_ctx", out int numCtx) is true ? numCtx : null,
        MinP = options.AdditionalProperties?.TryGetValue("min_p", out float minP) is true ? minP : null
    };

    /// <summary>
    /// Any declared function, invocable or not: a declaration-only tool is one the application calls itself.
    /// </summary>
    private static Schema.ToolDefinition ToInternalTool(this AIFunctionDeclaration function) => new(
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

    private static void MapToolCallsToContents(IEnumerable<Schema.ToolCall>? internalToolCalls, IList<AIContent> targetContents)
    {
        foreach (var toolCall in internalToolCalls ?? [])
        {
            var arguments = toolCall.Function.Arguments.ValueKind is not JsonValueKind.Undefined
                ? toolCall.Function.Arguments.Deserialize(ReadArguments)
                : null;

            targetContents.Add(new FunctionCallContent(
                callId: toolCall.Id ?? Guid.NewGuid().ToString("N"),
                name: toolCall.Function.Name,
                arguments: arguments));
        }
    }
}
