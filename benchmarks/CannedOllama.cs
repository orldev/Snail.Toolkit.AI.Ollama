using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Snail.Toolkit.AI.Ollama.Benchmarks;

/// <summary>
/// Ollama's answers as bytes held in memory, so that what is timed is the request built and serialised and the answer
/// parsed and mapped, never a network or a live server.
/// </summary>
internal static class CannedOllama
{
    public const string Model = "llama3.2:3b";

    /// <summary>A client whose every request is answered with the same body.</summary>
    public static HttpClient Answering(byte[] body) =>
        new(new Answer(body)) { BaseAddress = new Uri("http://ollama.local/") };

    /// <summary>A streamed chat: token chunks and the final one, one JSON object per line.</summary>
    public static byte[] ChatStream(int chunks)
    {
        var text = new StringBuilder();

        for (int index = 0; index < chunks; index++)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"{{\"model\":\"{Model}\",\"created_at\":\"2026-10-12T10:00:00.{123456789 + index}Z\",\"message\":{{\"role\":\"assistant\",\"content\":\" word{index}\"}},\"done\":false}}\n");
        }

        text.Append(CultureInfo.InvariantCulture,
            $"{{\"model\":\"{Model}\",\"created_at\":\"2026-10-12T10:00:01.123456789Z\",\"message\":{{\"role\":\"assistant\",\"content\":\"\"}},\"done\":true,\"done_reason\":\"stop\",\"total_duration\":4883583458,\"load_duration\":1334875,\"prompt_eval_count\":26,\"prompt_eval_duration\":342546000,\"eval_count\":282,\"eval_duration\":4535599000}}\n");

        return Encoding.UTF8.GetBytes(text.ToString());
    }

    /// <summary>One chat answer carrying a tool call.</summary>
    public static byte[] ChatAnswer()
    {
        var content = string.Join(' ', Enumerable.Repeat("The quick brown fox jumps over the lazy dog.", 20));

        return Encoding.UTF8.GetBytes(
            $"{{\"model\":\"{Model}\",\"created_at\":\"2026-10-12T10:00:01.123456789Z\",\"message\":{{\"role\":\"assistant\",\"content\":\"{content}\",\"tool_calls\":[{{\"function\":{{\"name\":\"get_weather\",\"arguments\":{{\"city\":\"Berlin\",\"unit\":\"celsius\",\"days\":3}}}}}}]}},\"done\":true,\"done_reason\":\"stop\",\"total_duration\":4883583458,\"load_duration\":1334875,\"prompt_eval_count\":26,\"prompt_eval_duration\":342546000,\"eval_count\":282,\"eval_duration\":4535599000}}");
    }

    /// <summary>Embeddings of <paramref name="inputs"/> texts in <paramref name="dimensions"/> dimensions.</summary>
    public static byte[] Embeddings(int inputs, int dimensions)
    {
        var random = new Random(42);
        var vectors = new float[inputs][];

        for (int input = 0; input < inputs; input++)
        {
            vectors[input] = new float[dimensions];

            for (int dimension = 0; dimension < dimensions; dimension++)
                vectors[input][dimension] = (float)((random.NextDouble() * 2) - 1);
        }

        var json = new Dictionary<string, object>
        {
            ["model"] = "nomic-embed-text",
            ["embeddings"] = vectors,
            ["prompt_eval_count"] = inputs * 12
        };

        return JsonSerializer.SerializeToUtf8Bytes(json);
    }

    /// <summary>A conversation of eighteen messages with tool calls and their results.</summary>
    public static List<ChatMessage> History()
    {
        List<ChatMessage> messages = [new(ChatRole.System, "You are a terse assistant.")];

        for (int index = 0; index < 4; index++)
        {
            var arguments = new Dictionary<string, object?> { ["city"] = "Berlin", ["days"] = 3, ["unit"] = "celsius" };
            var result = new Dictionary<string, object?> { ["temperature"] = 21.5, ["sky"] = "clear" };

            messages.Add(new ChatMessage(ChatRole.User, $"Question number {index}: what is the weather in Berlin for the next days?"));
            messages.Add(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"call{index}", "get_weather", arguments)]));
            messages.Add(new ChatMessage(ChatRole.Tool, [new FunctionResultContent($"call{index}", result)]));
            messages.Add(new ChatMessage(ChatRole.Assistant, "It will be clear and around 21 degrees."));
        }

        messages.Add(new ChatMessage(ChatRole.User, "Thanks. And tomorrow?"));

        return messages;
    }

    private sealed class Answer(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body),
                RequestMessage = request
            };

            response.Content.Headers.ContentType = new("application/json");

            return Task.FromResult(response);
        }
    }
}
