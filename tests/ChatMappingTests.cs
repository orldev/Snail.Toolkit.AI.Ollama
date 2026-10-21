using System.Text.Json;
using Microsoft.Extensions.AI;
using Snail.Toolkit.AI.Ollama.Contracts.Mapping;

using InternalChatResponse = Snail.Toolkit.AI.Ollama.Contracts.Responses.ChatResponse;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Unit tests for the mapping between MEAI abstractions and the internal Ollama wire contracts.
/// </summary>
public class ChatMappingTests
{
    private static InternalChatResponse Deserialize(string json) =>
        JsonSerializer.Deserialize<InternalChatResponse>(json, JsonSerializerOptions.Web)!;

    [Fact]
    public void ToAiResponse_MissingToolCallId_GeneratesCallId()
    {
        var response = Deserialize("""
            {"model":"qwen3","created_at":"2026-09-01T12:00:00Z",
             "message":{"role":"assistant","content":"","tool_calls":[
               {"function":{"name":"get_current_weather","arguments":{"location":"Paris"}}}]},
             "done":true,"done_reason":"stop","prompt_eval_count":120,"eval_count":30}
            """);

        var ai = response.ToAiResponse();

        var call = Assert.Single(ai.Messages.Single().Contents.OfType<FunctionCallContent>());
        Assert.False(string.IsNullOrWhiteSpace(call.CallId));
        Assert.Equal("get_current_weather", call.Name);
        var location = Assert.IsType<JsonElement>(call.Arguments!["location"]);
        Assert.Equal("Paris", location.GetString());
    }

    [Fact]
    public void ToAiResponse_OpenAiCompatibleToolCallId_PreservesCallId()
    {
        var response = Deserialize("""
            {"model":"m","message":{"role":"assistant","content":"","tool_calls":[
               {"id":"call_1","type":"function","function":{"name":"f","arguments":{}}}]},
             "done":true,"done_reason":"stop"}
            """);

        var call = Assert.Single(response.ToAiResponse().Messages.Single().Contents.OfType<FunctionCallContent>());
        Assert.Equal("call_1", call.CallId);
    }

    [Fact]
    public void ToAiResponse_CompletedResponse_MapsUsageAndFinishReason()
    {
        var response = Deserialize("""
            {"model":"m","message":{"role":"assistant","content":"Hi!"},
             "done":true,"done_reason":"stop","prompt_eval_count":120,"eval_count":30}
            """);

        var ai = response.ToAiResponse();

        Assert.Equal(ChatFinishReason.Stop, ai.FinishReason);
        Assert.Equal(120, ai.Usage!.InputTokenCount);
        Assert.Equal(30, ai.Usage.OutputTokenCount);
        Assert.Equal(150, ai.Usage.TotalTokenCount);
    }

    [Fact]
    public void ToAiResponse_ToolCallsWithStopDoneReason_ReportsToolCallsFinishReason()
    {
        var response = Deserialize("""
            {"model":"m","message":{"role":"assistant","content":"","tool_calls":[
               {"function":{"name":"f","arguments":{}}}]},
             "done":true,"done_reason":"stop"}
            """);

        Assert.Equal(ChatFinishReason.ToolCalls, response.ToAiResponse().FinishReason);
    }

    [Fact]
    public void ToAiResponse_LengthDoneReason_MapsLengthFinishReason()
    {
        var response = Deserialize("""
            {"model":"m","message":{"role":"assistant","content":"truncat"},
             "done":true,"done_reason":"length"}
            """);

        Assert.Equal(ChatFinishReason.Length, response.ToAiResponse().FinishReason);
    }

    [Fact]
    public void ToAiUpdate_StreamingChunk_OmitsUsageAndFinishReason()
    {
        var update = Deserialize("""
            {"model":"m","message":{"role":"assistant","content":"Hel"},"done":false}
            """).ToAiUpdate();

        Assert.Equal("Hel", update.Text);
        Assert.Null(update.FinishReason);
        Assert.Empty(update.Contents.OfType<UsageContent>());
    }

    [Fact]
    public void ToAiUpdate_FinalChunk_EmitsUsageAndFinishReason()
    {
        var update = Deserialize("""
            {"model":"m","message":{"role":"assistant","content":""},
             "done":true,"done_reason":"stop","prompt_eval_count":10,"eval_count":5}
            """).ToAiUpdate();

        Assert.Equal("m", update.ModelId);
        Assert.Equal(ChatFinishReason.Stop, update.FinishReason);
        var usage = Assert.Single(update.Contents.OfType<UsageContent>());
        Assert.Equal(15, usage.Details.TotalTokenCount);
    }

    [Fact]
    public void ToAiResponse_ThinkingPresent_MapsToReasoningContent()
    {
        var response = Deserialize("""
            {"model":"m","message":{"role":"assistant","content":"4","thinking":"2+2 is trivial"},
             "done":true,"done_reason":"stop"}
            """);

        var contents = response.ToAiResponse().Messages.Single().Contents;

        Assert.Equal("2+2 is trivial", Assert.Single(contents.OfType<TextReasoningContent>()).Text);
        Assert.Equal("4", Assert.Single(contents.OfType<TextContent>()).Text);
    }

    [Fact]
    public void ToInternalRequest_ImagesAndThinking_MapsToWireFields()
    {
        byte[] pixel = [0x89, 0x50, 0x4E, 0x47];
        List<ChatMessage> history =
        [
            new(ChatRole.User, [new TextContent("What is this?"), new DataContent(pixel, "image/png")]),
            new(ChatRole.Assistant, [new TextReasoningContent("Looks like a PNG header."), new TextContent("A PNG file.")])
        ];

        var json = JsonSerializer.SerializeToElement(
            history.ToInternalRequest(null, "m"), JsonSerializerOptions.Web);
        var messages = json.GetProperty("messages").EnumerateArray().ToArray();

        Assert.Equal(Convert.ToBase64String(pixel),
            messages[0].GetProperty("images").EnumerateArray().Single().GetString());
        Assert.Equal("Looks like a PNG header.", messages[1].GetProperty("thinking").GetString());
        Assert.Equal("A PNG file.", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public void ToInternalRequest_HistoryWithToolsAndResults_MapsAllFields()
    {
        var getWeather = AIFunctionFactory.Create(
            (string location) => location, "get_current_weather", "Gets the current weather");

        List<ChatMessage> history =
        [
            new(ChatRole.User, "Weather in Paris?"),
            new(ChatRole.Assistant,
            [
                new FunctionCallContent("abc123", "get_current_weather",
                    new Dictionary<string, object?> { ["location"] = "Paris" })
            ]),
            new(ChatRole.Tool, [new FunctionResultContent("abc123", "18C, sunny")])
        ];

        var request = history.ToInternalRequest(new ChatOptions { Tools = [getWeather] }, "qwen3");
        var json = JsonSerializer.SerializeToElement(request, JsonSerializerOptions.Web);

        Assert.Equal("qwen3", json.GetProperty("model").GetString());

        var messages = json.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(3, messages.Length);

        var toolCall = messages[1].GetProperty("tool_calls").EnumerateArray().Single();
        Assert.Equal("abc123", toolCall.GetProperty("id").GetString());
        Assert.Equal("get_current_weather", toolCall.GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("Paris", toolCall.GetProperty("function").GetProperty("arguments").GetProperty("location").GetString());

        var toolMessage = messages[2];
        Assert.Equal("tool", toolMessage.GetProperty("role").GetString());
        Assert.Equal("18C, sunny", toolMessage.GetProperty("content").GetString());
        Assert.Equal("abc123", toolMessage.GetProperty("tool_call_id").GetString());
        Assert.Equal("get_current_weather", toolMessage.GetProperty("tool_name").GetString());

        var toolDefinition = json.GetProperty("tools").EnumerateArray().Single();
        Assert.Equal("function", toolDefinition.GetProperty("type").GetString());
        Assert.Equal("get_current_weather", toolDefinition.GetProperty("function").GetProperty("name").GetString());
        Assert.NotEqual(JsonValueKind.Undefined, toolDefinition.GetProperty("function").GetProperty("parameters").ValueKind);
    }

    [Fact]
    public void ToInternalRequest_SamplingOptions_MapsAllValues()
    {
        var options = new ChatOptions
        {
            Temperature = 0.2f,
            MaxOutputTokens = 128,
            TopP = 0.9f,
            TopK = 40,
            Seed = 42,
            StopSequences = ["END"],
            PresencePenalty = 0.1f,
            FrequencyPenalty = 0.3f
        };

        var json = JsonSerializer.SerializeToElement(
            new List<ChatMessage> { new(ChatRole.User, "hi") }.ToInternalRequest(options, "m"),
            JsonSerializerOptions.Web);
        var modelOptions = json.GetProperty("options");

        Assert.Equal(0.2f, modelOptions.GetProperty("temperature").GetSingle());
        Assert.Equal(128, modelOptions.GetProperty("num_predict").GetInt32());
        Assert.Equal(42, modelOptions.GetProperty("seed").GetInt32());
        Assert.Equal("END", modelOptions.GetProperty("stop").EnumerateArray().Single().GetString());
        Assert.Equal(0.1f, modelOptions.GetProperty("presence_penalty").GetSingle());
        Assert.Equal(0.3f, modelOptions.GetProperty("frequency_penalty").GetSingle());
    }

    [Fact]
    public void ToInternalRequest_JsonSchemaResponseFormat_PassesSchemaThrough()
    {
        var schema = JsonSerializer.SerializeToElement(new { type = "object" });
        var options = new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema(schema) };

        var json = JsonSerializer.SerializeToElement(
            new List<ChatMessage> { new(ChatRole.User, "hi") }.ToInternalRequest(options, "m"),
            JsonSerializerOptions.Web);

        Assert.Equal("object", json.GetProperty("format").GetProperty("type").GetString());
    }

    [Fact]
    public void ToInternalRequest_PlainJsonResponseFormat_SendsJsonKeyword()
    {
        var options = new ChatOptions { ResponseFormat = ChatResponseFormat.Json };

        var json = JsonSerializer.SerializeToElement(
            new List<ChatMessage> { new(ChatRole.User, "hi") }.ToInternalRequest(options, "m"),
            JsonSerializerOptions.Web);

        Assert.Equal("json", json.GetProperty("format").GetString());
    }

    [Fact]
    public void ToInternalRequest_AdditionalProperties_MapsNumCtxMinPAndThink()
    {
        var options = new ChatOptions
        {
            AdditionalProperties = new()
            {
                ["think"] = "high",
                ["num_ctx"] = 8192,
                ["min_p"] = 0.05f
            }
        };

        var json = JsonSerializer.SerializeToElement(
            new List<ChatMessage> { new(ChatRole.User, "hi") }.ToInternalRequest(options, "m"),
            JsonSerializerOptions.Web);

        Assert.Equal("high", json.GetProperty("think").GetString());
        Assert.Equal(8192, json.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.Equal(0.05f, json.GetProperty("options").GetProperty("min_p").GetSingle());
    }

    [Fact]
    public void ToInternalRequest_BooleanThink_SerializesAsBoolean()
    {
        var options = new ChatOptions { AdditionalProperties = new() { ["think"] = true } };

        var json = JsonSerializer.SerializeToElement(
            new List<ChatMessage> { new(ChatRole.User, "hi") }.ToInternalRequest(options, "m"),
            JsonSerializerOptions.Web);

        Assert.True(json.GetProperty("think").GetBoolean());
    }

    [Fact]
    public void ToInternalRequest_RemoteImageUri_ThrowsNotSupportedException()
    {
        List<ChatMessage> history =
        [
            new(ChatRole.User, [new UriContent(new Uri("https://example.com/cat.png"), "image/png")])
        ];

        Assert.Throws<NotSupportedException>(() => history.ToInternalRequest(null, "m"));
    }

    [Fact]
    public void ToInternalRequest_ParallelToolResults_ExpandsIntoSeparateToolMessages()
    {
        List<ChatMessage> history =
        [
            new(ChatRole.Assistant,
            [
                new FunctionCallContent("id-1", "get_weather", new Dictionary<string, object?> { ["city"] = "Paris" }),
                new FunctionCallContent("id-2", "get_weather", new Dictionary<string, object?> { ["city"] = "Berlin" })
            ]),
            new(ChatRole.Tool,
            [
                new FunctionResultContent("id-1", "18C"),
                new FunctionResultContent("id-2", "21C")
            ])
        ];

        var json = JsonSerializer.SerializeToElement(
            history.ToInternalRequest(null, "m"), JsonSerializerOptions.Web);
        var messages = json.GetProperty("messages").EnumerateArray().ToArray();

        Assert.Equal(3, messages.Length);
        Assert.Equal(2, messages[0].GetProperty("tool_calls").GetArrayLength());

        var toolMessages = messages.Skip(1).ToArray();
        Assert.All(toolMessages, m => Assert.Equal("tool", m.GetProperty("role").GetString()));
        Assert.Equal(["18C", "21C"], toolMessages.Select(m => m.GetProperty("content").GetString()));
        Assert.Equal(["id-1", "id-2"], toolMessages.Select(m => m.GetProperty("tool_call_id").GetString()));
        Assert.All(toolMessages, m => Assert.Equal("get_weather", m.GetProperty("tool_name").GetString()));
    }

    [Fact]
    public void ToInternalRequest_MultipleTextContents_ConcatenatesText()
    {
        List<ChatMessage> history =
        [
            new(ChatRole.User, [new TextContent("Hello "), new TextContent("world")])
        ];

        var json = JsonSerializer.SerializeToElement(
            history.ToInternalRequest(null, "m"), JsonSerializerOptions.Web);

        Assert.Equal("Hello world",
            json.GetProperty("messages").EnumerateArray().Single().GetProperty("content").GetString());
    }

    [Fact]
    public void ToInternalRequest_NonStringToolResult_SerializesAsJson()
    {
        List<ChatMessage> history =
        [
            new(ChatRole.Assistant, [new FunctionCallContent("id-1", "get_weather")]),
            new(ChatRole.Tool,
            [
                new FunctionResultContent("id-1", new Dictionary<string, object?> { ["tempC"] = 18 })
            ])
        ];

        var json = JsonSerializer.SerializeToElement(
            history.ToInternalRequest(null, "m"), JsonSerializerOptions.Web);
        var toolMessage = json.GetProperty("messages").EnumerateArray().Last();

        var content = JsonDocument.Parse(toolMessage.GetProperty("content").GetString()!).RootElement;
        Assert.Equal(18, content.GetProperty("tempC").GetInt32());
    }

    private static JsonElement Wire(ChatOptions options) =>
        JsonSerializer.SerializeToElement(
            new List<ChatMessage> { new(ChatRole.User, "hi") }.ToInternalRequest(options, "m"),
            JsonSerializerOptions.Web);

    [Theory]
    [InlineData(ReasoningEffort.Low, "low")]
    [InlineData(ReasoningEffort.Medium, "medium")]
    [InlineData(ReasoningEffort.High, "high")]
    [InlineData(ReasoningEffort.ExtraHigh, "max")]
    public void ToInternalRequest_ReasoningEffort_SendsThinkLevel(ReasoningEffort effort, string level)
    {
        var json = Wire(new ChatOptions { Reasoning = new ReasoningOptions { Effort = effort } });

        Assert.Equal(level, json.GetProperty("think").GetString());
    }

    [Fact]
    public void ToInternalRequest_ReasoningEffortNone_TurnsThinkingOff()
    {
        var json = Wire(new ChatOptions { Reasoning = new ReasoningOptions { Effort = ReasoningEffort.None } });

        Assert.False(json.GetProperty("think").GetBoolean());
    }

    [Fact]
    public void ToInternalRequest_ExplicitThinkAndReasoning_ExplicitThinkWins()
    {
        var json = Wire(new ChatOptions
        {
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High },
            AdditionalProperties = new() { ["think"] = false }
        });

        Assert.False(json.GetProperty("think").GetBoolean());
    }

    /// <summary>
    /// A model without the thinking capability answers 400 to any think value, so none may be sent unasked.
    /// </summary>
    [Fact]
    public void ToInternalRequest_NoReasoningRequested_SendsNoThink()
    {
        var request = new List<ChatMessage> { new(ChatRole.User, "hi") }.ToInternalRequest(new ChatOptions(), "m");

        Assert.Null(request.Think);
    }

    [Fact]
    public void ToInternalRequest_Instructions_LeadAsSystemMessage()
    {
        var json = Wire(new ChatOptions { Instructions = "Answer in French." });

        var first = json.GetProperty("messages")[0];
        Assert.Equal("system", first.GetProperty("role").GetString());
        Assert.Equal("Answer in French.", first.GetProperty("content").GetString());
        Assert.Equal("user", json.GetProperty("messages")[1].GetProperty("role").GetString());
    }

    [Fact]
    public void ToInternalRequest_ToolModeNone_SendsNoTools()
    {
        var request = new List<ChatMessage> { new(ChatRole.User, "hi") }.ToInternalRequest(new ChatOptions
        {
            Tools = [AIFunctionFactory.Create(() => "18C", "get_weather")],
            ToolMode = ChatToolMode.None
        }, "m");

        Assert.Null(request.Tools);
    }

    /// <summary>
    /// MEAI 10 declares a tool the application calls itself as an AIFunctionDeclaration, the base of AIFunction;
    /// a mapping that kept only AIFunction sent "tools":[] and the model never learned the tool existed.
    /// </summary>
    [Fact]
    public void ToInternalRequest_DeclarationOnlyTool_IsOfferedToTheModel()
    {
        var declaration = AIFunctionFactory.Create((string city) => city, "get_weather", "Weather of a city").AsDeclarationOnly();

        var request = new List<ChatMessage> { new(ChatRole.User, "Weather?") }
            .ToInternalRequest(new ChatOptions { Tools = [declaration] }, "m");

        var tool = Assert.Single(request.Tools!);
        Assert.Equal("get_weather", tool.Function.Name);
        Assert.Equal("Weather of a city", tool.Function.Description);
        Assert.Equal(JsonValueKind.Object, tool.Function.Parameters.ValueKind);
    }

    [Fact]
    public void ToAiResponse_CachedPrompt_ReportsCachedInputTokens()
    {
        var response = Deserialize("""
            {"model":"m","message":{"role":"assistant","content":"ok"},"done":true,"done_reason":"stop",
             "prompt_eval_count":40,"prompt_eval_cached_count":14,"eval_count":3}
            """);

        Assert.Equal(14, response.ToAiResponse().Usage!.CachedInputTokenCount);
    }

    [Fact]
    public void ToInternalRequest_TopLogProbs_ImpliesLogProbs()
    {
        var json = Wire(new ChatOptions { AdditionalProperties = new() { ["top_logprobs"] = 3 } });

        Assert.True(json.GetProperty("logprobs").GetBoolean());
        Assert.Equal(3, json.GetProperty("top_logprobs").GetInt32());
    }

    [Fact]
    public void ToAiResponse_LogProbsReturned_ExposedInAdditionalProperties()
    {
        var response = Deserialize("""
            {"model":"m","message":{"role":"assistant","content":"Hi"},"done":true,"done_reason":"stop",
             "logprobs":[{"token":"Hi","logprob":-0.69,"bytes":[72,105],
               "top_logprobs":[{"token":"Hi","logprob":-0.69,"bytes":[72,105]},{"token":"Hello","logprob":-1.19}]}]}
            """);

        var logProbs = Assert.IsAssignableFrom<IReadOnlyList<Snail.Toolkit.AI.Ollama.Contracts.Schema.LogProbResult>>(
            response.ToAiResponse().AdditionalProperties!["logprobs"]);
        Assert.Equal("Hi", logProbs[0].Token);
        Assert.Equal("Hello", logProbs[0].TopLogprobs![1].Token);
    }

    [Fact]
    public void ToAiResponse_NoLogProbs_LeavesAdditionalPropertiesUnset()
    {
        var response = Deserialize("""{"model":"m","message":{"role":"assistant","content":"Hi"},"done":true}""");

        Assert.Null(response.ToAiResponse().AdditionalProperties);
    }

    /// <summary>
    /// Tool results and arguments are written and read as the reflection-based options wrote and read
    /// them: case-insensitive names, default naming and escaping, whatever the result's type.
    /// </summary>
    [Fact]
    public void ToInternalRequest_ResultsAndArgumentsOfManyTypes_WrittenAsBefore()
    {
        var before = OptionsBefore;
        var arguments = new Dictionary<string, object?>
        {
            ["city"] = "Zürich <\"quoted\">",
            ["days"] = 3,
            ["ratio"] = 0.75m,
            ["when"] = new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.FromHours(2)),
            ["tags"] = new[] { "a", "b" },
            ["nested"] = new Dictionary<string, object?> { ["empty"] = null, ["flag"] = true },
            ["poco"] = new Forecast("clear", 21.5),
            ["element"] = JsonDocument.Parse("""{"raw":[1,2]}""").RootElement
        };
        object[] results =
        [
            new Forecast("rain", 12.25),
            new { Temperature = 21.5, Sky = "clear" },
            new Dictionary<string, object?> { ["temperature"] = 21.5, ["sky"] = null },
            new List<int> { 1, 2, 3 },
            42,
            new TextContent("from a tool"),
            new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc)
        ];
        List<ChatMessage> history =
        [
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_weather", arguments)]),
            new(ChatRole.Tool, [.. results.Select((result, index) => new FunctionResultContent($"call-{index}", result))])
        ];

        var request = history.ToInternalRequest(null, "qwen3");
        var messages = request.Messages!.ToList();

        Assert.Equal(
            JsonSerializer.SerializeToElement<IDictionary<string, object?>>(arguments, before).GetRawText(),
            messages[0].ToolCalls!.Single().Function.Arguments.GetRawText());
        Assert.Equal(
            results.Select(result => JsonSerializer.Serialize(result, before)),
            messages.Skip(1).Select(message => message.Content));
    }

    [Fact]
    public void ToAiResponse_ToolCallArguments_ReadAsBefore()
    {
        const string arguments = """{"location":"Paris","days":3,"nested":{"a":[1,null]},"none":null}""";
        var response = Deserialize($$$"""
            {"model":"m","message":{"role":"assistant","content":"","tool_calls":[
              {"function":{"name":"get_weather","arguments":{{{arguments}}}}}]},"done":true}
            """);

        var call = Assert.Single(response.ToAiResponse().Messages.Single().Contents.OfType<FunctionCallContent>());
        var expected = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments, OptionsBefore)!;

        Assert.Equal(
            expected.Select(pair => $"{pair.Key}={pair.Value?.GetType().Name}:{pair.Value}"),
            call.Arguments!.Select(pair => $"{pair.Key}={pair.Value?.GetType().Name}:{pair.Value}"));
    }

    private static readonly JsonSerializerOptions OptionsBefore = new() { PropertyNameCaseInsensitive = true };

    private sealed record Forecast(string Sky, double Temperature);
}
