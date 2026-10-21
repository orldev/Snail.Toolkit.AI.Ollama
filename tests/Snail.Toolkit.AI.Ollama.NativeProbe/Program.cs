using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snail.Toolkit.AI.Ollama;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;

using IChatClient = Snail.Toolkit.AI.Ollama.Abstractions.IChatClient;

var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Ollama:BaseUrl"] = "http://probe.local/ollama",
        ["Ollama:DefaultModel"] = "llama3.2",
        ["Ollama:Timeout"] = "00:00:30"
    })
    .Build();

var services = new ServiceCollection();
services.AddOllama(configuration.GetSection("Ollama"));

foreach (var name in new[] { nameof(IChatClient), nameof(IEmbeddingsClient) })
{
    services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new Answers());
}

await using var provider = services.BuildServiceProvider();
var ollama = provider.GetRequiredService<IOllamaClient>();

List<ChatMessage> history =
[
    new(ChatRole.User, "What is the weather in Berlin?"),
    new(ChatRole.Assistant, [new FunctionCallContent("call1", "get_weather", new Dictionary<string, object?> { ["city"] = "Berlin", ["days"] = 3 })]),
    new(ChatRole.Tool, [new FunctionResultContent("call1", "clear, 21 degrees")]),
    new(ChatRole.User, "And tomorrow?")
];

var answer = await ollama.Chats.GetResponseAsync(history, new ChatOptions
{
    Temperature = 0.2f,
    ResponseFormat = ChatResponseFormat.Json,
    AdditionalProperties = new() { ["think"] = "low" }
});
var call = answer.Messages[0].Contents.OfType<FunctionCallContent>().Single();

List<ChatResponseUpdate> updates = [];

await foreach (var update in ollama.Chats.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Hi")]))
{
    updates.Add(update);
}

var embeddings = await ollama.Embeddings.GenerateAsync(["one", "two"]);
var tuned = await ollama.Embeddings.GenerateAsync(
    new EmbeddingsRequest("llama3.2", ["three"], new Dictionary<string, object?> { ["num_ctx"] = 512 }));

var isChatted = answer.Text == "Tomorrow too." && answer.FinishReason == ChatFinishReason.ToolCalls
    && call is { Name: "get_weather" } && call.Arguments?["city"]?.ToString() == "Paris"
    && Answers.Chatted.Contains("\"tool_calls\":[{\"id\":\"call1\",\"type\":\"function\",\"function\":{\"name\":\"get_weather\",\"arguments\":{\"city\":\"Berlin\",\"days\":3}}}]", StringComparison.Ordinal)
    && Answers.Chatted.Contains("\"tool_name\":\"get_weather\"", StringComparison.Ordinal)
    && Answers.Chatted.Contains("\"options\":{\"temperature\":0.2}", StringComparison.Ordinal)
    && Answers.Chatted.Contains("\"format\":\"json\"", StringComparison.Ordinal)
    && Answers.Chatted.Contains("\"think\":\"low\"", StringComparison.Ordinal);
var isStreamed = string.Concat(updates.Select(update => update.Text)) == "Hello there"
    && updates[^1].FinishReason == ChatFinishReason.Stop
    && updates[^1].Contents.OfType<UsageContent>().Single().Details.TotalTokenCount == 9;
var isEmbedded = embeddings.Count == 2 && embeddings[1].Vector.Span[2] == 0.5f && embeddings.Usage?.InputTokenCount == 4
    && tuned.Embeddings.Length == 2
    && Answers.Embedded == """{"model":"llama3.2","input":["three"],"options":{"num_ctx":512}}""";

Console.WriteLine($"chatted {isChatted}, streamed {isStreamed}, embedded {isEmbedded}");

return isChatted && isStreamed && isEmbedded ? 0 : 1;

/// <summary>Ollama's answers held in memory, by path, under the base address the configuration names.</summary>
internal sealed class Answers : HttpMessageHandler
{
    public static string Chatted { get; private set; } = string.Empty;

    public static string Embedded { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sent = await request.Content!.ReadAsStringAsync(cancellationToken);
        var isStreamed = sent.Contains("\"stream\":true", StringComparison.Ordinal);

        var body = request.RequestUri!.AbsoluteUri switch
        {
            "http://probe.local/ollama/api/chat" when isStreamed =>
                """
                {"model":"llama3.2","created_at":"2026-10-13T10:00:00Z","message":{"role":"assistant","content":"Hello"},"done":false}
                {"model":"llama3.2","created_at":"2026-10-13T10:00:00Z","message":{"role":"assistant","content":" there"},"done":false}
                {"model":"llama3.2","created_at":"2026-10-13T10:00:01Z","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":7,"eval_count":2}

                """,
            "http://probe.local/ollama/api/chat" =>
                """{"model":"llama3.2","created_at":"2026-10-13T10:00:01Z","message":{"role":"assistant","content":"Tomorrow too.","tool_calls":[{"function":{"name":"get_weather","arguments":{"city":"Paris","days":1}}}]},"done":true,"done_reason":"stop"}""",
            "http://probe.local/ollama/api/embed" =>
                """{"model":"llama3.2","embeddings":[[0.1,0.2,0.3],[0.3,0.4,0.5]],"prompt_eval_count":4}""",
            _ => null
        };

        if (request.RequestUri.AbsolutePath.EndsWith("/api/chat", StringComparison.Ordinal) && !isStreamed)
        {
            Chatted = sent;
        }

        if (request.RequestUri.AbsolutePath.EndsWith("/api/embed", StringComparison.Ordinal))
        {
            Embedded = sent;
        }

        return new HttpResponseMessage(body is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
        {
            Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json"),
            RequestMessage = request
        };
    }
}
