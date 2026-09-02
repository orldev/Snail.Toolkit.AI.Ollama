# Snail.Toolkit.AI.Ollama

A [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) (MEAI) provider for [Ollama](https://ollama.com)'s native API. Chat with tool calling, thinking and images, NDJSON streaming, structured output and embeddings — all composable with the standard MEAI pipeline (`FunctionInvokingChatClient`, telemetry, caching).

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

This registers `IOllamaClient` (the facade) plus the individual `IChatClient`, `IGenerateClient` and `IEmbeddingsClient`.

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

Reasoning of thinking-capable models arrives as `TextReasoningContent`. Control it — and other Ollama-specific knobs — through `AdditionalProperties`:

```csharp
var options = new ChatOptions
{
    AdditionalProperties = new()
    {
        ["think"] = "high",   // boolean or "low" / "medium" / "high" / "max"
        ["num_ctx"] = 16384,  // raise the context window; images consume it fast
        ["min_p"] = 0.05f
    }
};
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

Embedding calls are retried on transient failures; streaming calls never are.

## Single-shot generation

```csharp
var request = new GenerateRequest("qwen3", "Write a haiku about the sea.");

await foreach (var chunk in ollama.Generate.StreamAsync(request))
{
    Console.Write(chunk.Content);
}
```

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
| `BaseUrl` | `http://localhost:11434` | Point at `https://ollama.com` for Ollama Cloud. |
| `ApiKey` | `null` | Sent as a Bearer token when set; a local Ollama needs none. |
| `DefaultModel` | `null` | Fallback model; without it every call must set `ChatOptions.ModelId`. |
| `Timeout` | 100 s | Bounds unary calls only. Streams run until done or the caller's token fires. |

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

## Testing

Unit tests run offline. Live integration tests are opt-in via environment variables:

```bash
OLLAMA_URL=http://localhost:11434 OLLAMA_MODEL=qwen3 dotnet test          # tool-calling loop
OLLAMA_VISION_MODEL=qwen2.5vl dotnet test                                  # vision + structured output
```

## License

Snail.Toolkit.AI.Ollama is a free and open source project, released under the permissible [MIT license](LICENSE).
