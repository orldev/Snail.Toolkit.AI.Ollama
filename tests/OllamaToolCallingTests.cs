using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Marks a fact that only runs when a live Ollama endpoint is available via the
/// OLLAMA_URL environment variable (OLLAMA_MODEL selects a tool-capable model,
/// default "qwen3").
/// </summary>
public sealed class OllamaFactAttribute : FactAttribute
{
    public OllamaFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_URL")))
        {
            Skip = "Set OLLAMA_URL (and optionally OLLAMA_MODEL) to run Ollama integration tests.";
        }
    }
}

/// <summary>
/// Live verification of the MEAI tool-calling path over Ollama:
/// FunctionInvokingChatClient -> ChatOptions.Tools -> tool call -> tool result -> answer,
/// with usage reported at the end of the step.
/// </summary>
public class OllamaToolCallingTests
{
    private static string BaseUrl => Environment.GetEnvironmentVariable("OLLAMA_URL")!;
    private static string Model => Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3";

    private static (IChatClient Client, ChatOptions Options, Func<bool> Invoked) CreateClient()
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
        var inner = new ChatClient(httpClient,
            Options.Create(new OllamaOptions { BaseUrl = BaseUrl, DefaultModel = Model }));

        var invoked = false;
        var getWeather = AIFunctionFactory.Create(
            (string location) =>
            {
                invoked = true;
                return $"18 degrees Celsius and sunny in {location}";
            },
            "get_current_weather", "Gets the current weather for a location");

        var client = new ChatClientBuilder(inner).UseFunctionInvocation().Build();
        var options = new ChatOptions { ModelId = Model, Tools = [getWeather] };
        return (client, options, () => invoked);
    }

    private static ChatMessage CreatePrompt() =>
        new(ChatRole.User, "What is the weather in Paris right now? Use the get_current_weather tool.");

    [OllamaFact]
    public async Task GetResponseAsync_LiveOllamaToolLoop_CompletesWithUsage()
    {
        var (client, options, invoked) = CreateClient();
        using var _ = client;

        var response = await client.GetResponseAsync([CreatePrompt()], options);

        Assert.True(invoked(), "The model never called get_current_weather.");
        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.True(response.Usage is { TotalTokenCount: > 0 }, "Usage was not reported.");
    }

    [OllamaFact]
    public async Task GetStreamingResponseAsync_LiveOllamaToolLoop_CompletesWithUsage()
    {
        var (client, options, invoked) = CreateClient();
        using var _ = client;

        var response = await client
            .GetStreamingResponseAsync([CreatePrompt()], options)
            .ToChatResponseAsync();

        Assert.True(invoked(), "The model never called get_current_weather.");
        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.True(response.Usage is { TotalTokenCount: > 0 }, "Usage was not reported.");
    }
}
