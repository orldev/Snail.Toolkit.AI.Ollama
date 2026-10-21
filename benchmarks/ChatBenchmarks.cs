using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.AI;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Benchmarks;

/// <summary>
/// The chat hot paths: a streamed answer of 200 token chunks parsed line by line into updates, and one answer to an
/// eighteen-message history with tool calls, the request mapped and serialised and the answer parsed.
/// </summary>
[MemoryDiagnoser]
public class ChatBenchmarks
{
    private static readonly ChatOptions Options = new() { ModelId = CannedOllama.Model, Temperature = 0.2f };

    private readonly List<ChatMessage> _prompt = [new(ChatRole.User, "Tell me a story.")];
    private readonly List<ChatMessage> _history = CannedOllama.History();

    private ChatClient _streaming = null!;
    private ChatClient _unary = null!;

    [GlobalSetup]
    public void Setup()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OllamaOptions());

        _streaming = new ChatClient(CannedOllama.Answering(CannedOllama.ChatStream(200)), options);
        _unary = new ChatClient(CannedOllama.Answering(CannedOllama.ChatAnswer()), options);
    }

    [Benchmark]
    public async Task<int> Streamed()
    {
        int contents = 0;

        await foreach (var update in _streaming.GetStreamingResponseAsync(_prompt, Options))
            contents += update.Contents.Count;

        return contents;
    }

    [Benchmark]
    public Task<ChatResponse> WithTools() => _unary.GetResponseAsync(_history, Options);
}
