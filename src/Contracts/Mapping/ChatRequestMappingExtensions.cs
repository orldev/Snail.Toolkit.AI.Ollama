using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Snail.Toolkit.AI.Ollama.Contracts.Mapping;

/// <summary>
/// Maps a MEAI conversation and its options to Ollama's /api/chat request.
/// </summary>
internal static class ChatRequestMappingExtensions
{
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
            KeepAlive = options.ToKeepAlive(),
            TopLogProbs = options.ToTopLogProbs(),
            LogProbs = options.ToLogProbs(),
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
    /// An explicit <see cref="OllamaProperties.Think"/> wins; otherwise the MEAI reasoning effort picks the level.
    /// </summary>
    /// <remarks>
    /// Measured on Ollama 0.35.0: a model whose /api/show lists only [false, true] still accepts "low",
    /// "medium" and "max" and simply thinks, while a model without the thinking capability rejects any
    /// think value with 400 — so nothing is sent unless the caller asked for reasoning.
    /// </remarks>
    private static object? ToThink(this ChatOptions? options) =>
        options?.AdditionalProperties?.GetValueOrDefault(OllamaProperties.Think)
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
        options?.AdditionalProperties?.TryGetValue(OllamaProperties.TopLogProbs, out int top) is true ? top : null;

    /// <summary>
    /// An explicit <see cref="OllamaProperties.LogProbs"/> wins; asking for alternatives turns them on.
    /// </summary>
    private static bool? ToLogProbs(this ChatOptions? options)
    {
        if (options?.AdditionalProperties?.TryGetValue(OllamaProperties.LogProbs, out bool logProbs) is true)
        {
            return logProbs;
        }

        return options.ToTopLogProbs() is not null ? true : null;
    }

    /// <summary>
    /// How long Ollama keeps the model loaded after the call; unset leaves the server's OLLAMA_KEEP_ALIVE.
    /// </summary>
    private static string? ToKeepAlive(this ChatOptions? options) =>
        options?.AdditionalProperties?.TryGetValue(OllamaProperties.KeepAlive, out string? keepAlive) is true ? keepAlive : null;

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
                    Arguments: JsonSerializer.SerializeToElement(call.Arguments, ToolJson.WrittenArguments))))
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
        _ => JsonSerializer.Serialize(result, ToolJson.WrittenResult)
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
        NumCtx = options.AdditionalProperties?.TryGetValue(OllamaProperties.NumCtx, out int numCtx) is true ? numCtx : null,
        MinP = options.AdditionalProperties?.TryGetValue(OllamaProperties.MinP, out float minP) is true ? minP : null
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
}
