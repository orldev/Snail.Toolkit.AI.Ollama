using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Contracts.Mapping;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Guard tests: every key <see cref="OllamaProperties"/> names reaches the Ollama field it is named after, and the
/// README documents every one of them.
/// </summary>
public class OllamaPropertiesTests
{
    /// <summary>The chat keys, a value Ollama accepts and the field of the request the key must set.</summary>
    public static TheoryData<string, object, string> ChatKeys => new()
    {
        { OllamaProperties.Think, "high", "think" },
        { OllamaProperties.KeepAlive, "0", "keep_alive" },
        { OllamaProperties.LogProbs, true, "logprobs" },
        { OllamaProperties.TopLogProbs, 3, "top_logprobs" },
        { OllamaProperties.NumCtx, 16384, "options.num_ctx" },
        { OllamaProperties.MinP, 0.05f, "options.min_p" }
    };

    private static IEnumerable<FieldInfo> Keys() =>
        typeof(OllamaProperties).GetFields(BindingFlags.Public | BindingFlags.Static).Where(field => field.IsLiteral);

    [Theory]
    [MemberData(nameof(ChatKeys))]
    public void ChatKey_Set_ReachesTheFieldItNames(string key, object value, string field)
    {
        var request = new List<ChatMessage> { new(ChatRole.User, "hi") }
            .ToInternalRequest(new ChatOptions { AdditionalProperties = new() { [key] = value } }, "m");

        using var body = JsonDocument.Parse(JsonSerializer.Serialize(request, Wire.ChatRequest));

        var reached = field.Split('.').Aggregate(body.RootElement, (element, name) => element.GetProperty(name));
        Assert.NotEqual(JsonValueKind.Null, reached.ValueKind);
        Assert.Equal(field.Split('.')[^1], key);
    }

    /// <summary>
    /// A key added without a guard would be one more string nothing checks; Truncate is guarded by the embeddings tests.
    /// </summary>
    [Fact]
    public void EveryKey_IsGuarded()
    {
        var guarded = ChatKeys.Select(row => (string)row[0]).Append(OllamaProperties.Truncate).ToHashSet(StringComparer.Ordinal);

        var keys = Keys().Select(field => (string)field.GetRawConstantValue()!);

        Assert.All(keys, key => Assert.Contains(key, guarded));
    }

    [Fact]
    public void EveryKey_IsDocumentedInTheReadme()
    {
        string root = AppContext.BaseDirectory;

        while (!File.Exists(Path.Combine(root, "global.json")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        string readme = File.ReadAllText(Path.Combine(root, "README.md"));

        Assert.All(Keys(), field => Assert.Contains($"OllamaProperties.{field.Name}", readme, StringComparison.Ordinal));
    }
}
