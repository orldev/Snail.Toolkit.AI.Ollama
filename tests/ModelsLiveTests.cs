using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Live verification of the model queries against a real server; OLLAMA_MODEL must be pulled there and
/// support tools.
/// </summary>
public class ModelsLiveTests
{
    private static ModelsClient CreateClient()
    {
        string baseUrl = Environment.GetEnvironmentVariable("OLLAMA_URL")!;

        return new ModelsClient(
            new HttpClient { BaseAddress = new Uri(baseUrl) },
            Options.Create(new OllamaOptions { BaseUrl = baseUrl }));
    }

    private static string Model => Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3";

    [OllamaFact]
    public async Task GetVersionAsync_LiveServer_ReturnsVersion()
    {
        string version = await CreateClient().GetVersionAsync();

        Assert.Matches(@"^\d+\.\d+\.\d+", version);
    }

    [OllamaFact]
    public async Task ListAsync_LiveServer_ContainsTheConfiguredModel()
    {
        var models = await CreateClient().ListAsync();

        Assert.Contains(models, model => model.Name == Model);
    }

    [OllamaFact]
    public async Task ShowAsync_ConfiguredModel_SupportsTools()
    {
        var description = await CreateClient().ShowAsync(Model);

        Assert.True(description!.Supports("tools"));
        Assert.True(description.ContextLength > 0);
    }

    [OllamaFact]
    public async Task ShowAsync_UnknownModel_ReturnsNull()
    {
        Assert.Null(await CreateClient().ShowAsync("snail-toolkit-missing-model"));
    }

    [OllamaFact]
    public async Task ListRunningAsync_LiveServer_Answers()
    {
        var running = await CreateClient().ListRunningAsync();

        Assert.All(running, model => Assert.False(string.IsNullOrEmpty(model.Name)));
    }
}
