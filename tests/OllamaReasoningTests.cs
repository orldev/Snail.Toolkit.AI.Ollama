using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Live verification that MEAI's reasoning effort switches a thinking model's reasoning on and off.
/// OLLAMA_MODEL must name a model with the thinking capability.
/// </summary>
public class OllamaReasoningTests
{
    private static ChatClient CreateClient()
    {
        string baseUrl = Environment.GetEnvironmentVariable("OLLAMA_URL")!;

        return new ChatClient(
            new HttpClient { BaseAddress = new Uri(baseUrl) },
            Options.Create(new OllamaOptions
            {
                BaseUrl = baseUrl,
                DefaultModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3",
                Timeout = TimeSpan.FromMinutes(5)
            }));
    }

    private static Task<ChatResponse> AskAsync(ReasoningEffort effort) =>
        CreateClient().GetResponseAsync(
            [new ChatMessage(ChatRole.User, "What is 2 + 2? Answer with one word.")],
            new ChatOptions { MaxOutputTokens = 800, Reasoning = new ReasoningOptions { Effort = effort } });

    [OllamaFact]
    public async Task GetResponseAsync_ReasoningEffortNone_ReturnsNoReasoning()
    {
        var response = await AskAsync(ReasoningEffort.None);

        Assert.Empty(response.Messages.SelectMany(message => message.Contents).OfType<TextReasoningContent>());
        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    [OllamaFact]
    public async Task GetResponseAsync_ReasoningEffortMedium_ReturnsReasoning()
    {
        var response = await AskAsync(ReasoningEffort.Medium);

        Assert.NotEmpty(response.Messages.SelectMany(message => message.Contents).OfType<TextReasoningContent>());
    }
}
