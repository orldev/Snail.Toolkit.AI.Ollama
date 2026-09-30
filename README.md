# Snail.Toolkit.AI.Ollama

A [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) (MEAI) provider for [Ollama](https://ollama.com)'s native API. Chat with tool calling, thinking and images, NDJSON streaming, structured output, embeddings, System One scoring and model queries — all composable with the standard MEAI pipeline (`FunctionInvokingChatClient`, telemetry, caching).

Works with a local Ollama and with [Ollama Cloud](https://ollama.com) (set `BaseUrl` and `ApiKey`).

## Installation

```bash
dotnet add package Snail.Toolkit.AI.Ollama
```

Register the feature:

```csharp
services.AddOllama(options =>
{
    options.BaseUrl = "http://localhost:11434";
    options.DefaultModel = "qwen3";
});

// or bind from configuration
services.AddOllama(builder.Configuration.GetSection("Ollama"));
```

This registers `IOllamaClient` (the facade) plus the individual `IChatClient`, `IGenerateClient`, `IEmbeddingsClient`, `ISystemOneClient` and `IModelsClient`.

## Chat: the MEAI surface

`Chats` implements `Microsoft.Extensions.AI.IChatClient`, so everything from the MEAI ecosystem plugs in directly.

```csharp
var response = await ollama.Chats.GetResponseAsync(
    [new ChatMessage(ChatRole.User, "What is the capital of France?")]);

Console.WriteLine(response.Text);
Console.WriteLine(response.Usage?.TotalTokenCount);
```

### Streaming

```csharp
await foreach (var update in ollama.Chats.GetStreamingResponseAsync(messages))
{
    Console.Write(update.Text);
}
```

The final update carries `FinishReason` and a `UsageContent` with token counts.

### Tool calling

Wrap the client in MEAI's function invoker and hand it your tools — the loop, including parallel tool calls, is handled for you:

```csharp
var getWeather = AIFunctionFactory.Create(
    (string location) => $"18°C and sunny in {location}",
    "get_current_weather", "Gets the current weather for a location");

using var agent = new ChatClientBuilder(ollama.Chats)
    .UseFunctionInvocation()
    .Build();

var answer = await agent.GetResponseAsync(
    [new ChatMessage(ChatRole.User, "Weather in Paris?")],
    new ChatOptions { Tools = [getWeather] });
```

### Structured output

```csharp
var schema = JsonSerializer.SerializeToElement(new
{
    type = "object",
    properties = new { capital = new { type = "string" } },
    required = new[] { "capital" }
});

var options = new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema(schema) };
```

### Images and thinking

Attach images as `DataContent` (Ollama accepts only inline base64 — a remote `UriContent` throws):

```csharp
var message = new ChatMessage(ChatRole.User,
[
    new TextContent("What is in this picture?"),
    new DataContent(imageBytes, "image/png")
]);
```

Reasoning of thinking-capable models arrives as `TextReasoningContent`. The standard MEAI options map onto Ollama:

```csharp
var options = new ChatOptions
{
    Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Medium }, // None → think off, ExtraHigh → "max"
    Instructions = "Answer in French.",                                    // sent as a leading system message
    ToolMode = ChatToolMode.None                                           // tools are not offered at all
};
```

No `think` is sent unless you ask for reasoning: a model without the thinking capability answers 400 to any value. Usage reports `CachedInputTokenCount` from Ollama's prompt cache.

Ollama-specific knobs go through `AdditionalProperties`; an explicit `think` there wins over `Reasoning`:

```csharp
var options = new ChatOptions
{
    AdditionalProperties = new()
    {
        ["think"] = "high",      // boolean or a level the model accepts
        ["num_ctx"] = 16384,     // raise the context window; images consume it fast
        ["min_p"] = 0.05f,
        ["keep_alive"] = "0",    // unload right after; unset leaves the server's OLLAMA_KEEP_ALIVE
        ["top_logprobs"] = 3     // 0–20 alternatives per token; implies ["logprobs"] = true
    }
};

var logProbs = (IReadOnlyList<LogProbResult>)response.AdditionalProperties!["logprobs"];
```

## Embeddings

`Embeddings` implements MEAI's `IEmbeddingGenerator<string, Embedding<float>>`, so it drops into vector stores and semantic search components like any other provider:

```csharp
var embeddings = await ollama.Embeddings.GenerateAsync(["The cat is on the mat"]);

foreach (var embedding in embeddings)
{
    Console.WriteLine(embedding.Vector.Length);
}
```

Shorten vectors of models trained for it with `Dimensions`, and make overlong input fail instead of being silently cut:

```csharp
var embeddings = await ollama.Embeddings.GenerateAsync(["…"], new EmbeddingGenerationOptions
{
    Dimensions = 256,
    AdditionalProperties = new() { ["truncate"] = false }   // 400 instead of a vector of the first lines
});
```

Embedding calls are retried on transient failures; streaming calls never are.

## Single-shot generation

```csharp
var request = new GenerateRequest("qwen3", "Write a haiku about the sea.")
{
    System = "You are a poet.",   // also Suffix for fill-in-the-middle, Raw to bypass the template
    Think = true,
    LogProbs = true
};

await foreach (var chunk in ollama.Generate.StreamAsync(request))
{
    Console.Write(chunk.Thinking ?? chunk.Content);

    if (chunk.IsDone)
    {
        Console.WriteLine($"\n{chunk.DoneReason}: {chunk.Usage?.OutputTokenCount} tokens");
    }
}
```

## System One: choices, yes/no and scores

[System One](https://docs.ollama.com/api/systemone) answers classification questions with probabilities instead of generated text. Ask several named questions about one state in a single call; each is scored independently:

```csharp
var response = await ollama.SystemOne.AnswerAsync(new SystemOneRequest(
    Model: "nimble",
    State: "Our checkout has returned 500 errors since 9am.",
    Questions: new Dictionary<string, SystemOneQuestion>
    {
        ["label"] = new ChoiceQuestion("Which label fits this ticket?", new Dictionary<string, string?>
        {
            ["billing"] = "Payments and refunds",
            ["bug"] = "Software errors",
            ["account"] = "Login and account access"
        }),
        ["urgent"] = new NoulQuestion("Does this need someone right now?"),
        ["severity"] = new ScoreQuestion("How severe is it?", ["minor", "major", "outage"])
    }));

if (response.Answers["label"] is ChoiceAnswer label)
{
    Console.WriteLine($"{label.Choice} ({label.Confidence:P0} confident)");
}
```

Each answer comes back as the kind it was asked as: `ChoiceAnswer` (the pick, every option's probability and a confidence), `NoulAnswer` (the probability of yes, so you choose the threshold) or `ScoreAnswer` (a probability-weighted index from 0 to N − 1 — not normalized to 0–1).

Requires Ollama 0.35.0+ and a local GGUF model trained for System One. The call never streams, is bounded by `Timeout` and is retried on transient failures. Oversized input is never truncated: a request over 64 KiB or a prompt that overflows the context window fails with `HttpBuilderException`.

## Models

Ask the server what it runs before relying on it:

```csharp
string version = await ollama.Models.GetVersionAsync();              // "0.35.0"
var pulled = await ollama.Models.ListAsync();                        // /api/tags
var loaded = await ollama.Models.ListRunningAsync();                 // /api/ps: memory, VRAM, expiry

var model = await ollama.Models.ShowAsync("nimble");                 // null when the server lacks it
if (model?.Supports("decision") is true)
{
    // safe to call System One
}
```

`ShowAsync` also reports the context window (`ContextLength`), the think values a model accepts (`Thinking`) and its template.

The window is read from the key named after `general.architecture` first, then from any `*.context_length`: Ollama sorts
`model_info` keys, and a projector's window can sort ahead of the model's own. Capabilities are advisory and read
leniently — an entry that is not a name is skipped, and a value that is not a list reads as `null` — so one odd entry
never costs you the window, the template and the think values of the same answer.

## Buffering for UIs

Token-by-token updates are often too chatty to render. Coalesce them:

```csharp
await foreach (var block in ollama.Chats
    .GetStreamingResponseAsync(messages)
    .BufferTextAsync(chunkSize: 50))
{
    Render(block);
}
```

## Options

| Option | Default | Meaning |
| :--- | :--- | :--- |
| `BaseUrl` | `http://localhost:11434` | Point at `https://ollama.com` for Ollama Cloud. A path prefix behind a proxy (`https://gateway/ollama`) is kept. |
| `ApiKey` | `null` | Sent as a Bearer token when set; a local Ollama needs none. |
| `DefaultModel` | `null` | Fallback model; without it every call must set `ChatOptions.ModelId`. |
| `Timeout` | 100 s | Bounds unary calls, retries included. Streams run until done or the caller's token fires. |

## Error handling

A non-success status surfaces as `HttpBuilderException` carrying the method, URI, status code and the response body — the actual server error is never lost:

```csharp
try
{
    var response = await ollama.Chats.GetResponseAsync(messages);
}
catch (HttpBuilderException ex)
{
    Console.WriteLine($"Ollama returned {ex.StatusCode}: {ex.Body}");
}
```

A missing model configuration fails fast with `InvalidOperationException` before any request is sent.

A unary call that outlives `Timeout` throws `TimeoutException`; cancelling your own token still throws `OperationCanceledException`, so the two are easy to tell apart.

Streams never end quietly on a failure. Once the 200 headers are out, Ollama can only report an error as a line of the stream: that surfaces as `InvalidOperationException` carrying Ollama's own message. A stream that closes before its final chunk — a crashed runner, a proxy cutting the connection — throws `IOException` instead of handing you a truncated answer.

## Testing

Unit tests run offline. Live integration tests are opt-in via environment variables:

```bash
OLLAMA_URL=http://localhost:11434 OLLAMA_MODEL=qwen3 dotnet test          # tools, reasoning, generate, logprobs, models
OLLAMA_VISION_MODEL=qwen2.5vl dotnet test                                  # vision + structured output
OLLAMA_URL=http://localhost:11434 OLLAMA_SYSTEMONE_MODEL=nimble dotnet test # System One, Ollama 0.35.0+
OLLAMA_URL=http://localhost:11434 OLLAMA_EMBED_MODEL=qwen3-embedding:0.6b dotnet test # dimensions, truncate
```

## License

Snail.Toolkit.AI.Ollama is a free and open source project, released under the permissible [MIT license](LICENSE).
