using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Marks a fact that only runs against a live vision-capable Ollama model:
/// requires both OLLAMA_URL and OLLAMA_VISION_MODEL environment variables.
/// </summary>
public sealed class OllamaVisionFactAttribute : FactAttribute
{
    public OllamaVisionFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_URL")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_VISION_MODEL")))
        {
            Skip = "Set OLLAMA_URL and OLLAMA_VISION_MODEL to run vision integration tests.";
        }
    }
}

/// <summary>
/// Live verification of the multimodal path: image input plus structured output.
/// </summary>
public class OllamaVisionTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private static readonly string[] RequiredProperties = ["color"];

    [OllamaVisionFact]
    public async Task GetResponseAsync_ImageWithJsonSchema_ReturnsStructuredAnswer()
    {
        var baseUrl = Environment.GetEnvironmentVariable("OLLAMA_URL")!;
        var model = Environment.GetEnvironmentVariable("OLLAMA_VISION_MODEL")!;

        using var client = new ChatClient(
            new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = Timeout.InfiniteTimeSpan },
            Options.Create(new OllamaOptions
            {
                BaseUrl = baseUrl,
                DefaultModel = model,
                Timeout = TimeSpan.FromMinutes(5)
            }));

        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { color = new { type = "string" } },
            required = RequiredProperties
        });

        ChatMessage prompt = new(ChatRole.User,
        [
            new TextContent("What color dominates this image? Answer as JSON."),
            new DataContent(OnePixelPng, "image/png")
        ]);

        var response = await client.GetResponseAsync(
            [prompt],
            new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema(schema) });

        var json = JsonDocument.Parse(response.Text).RootElement;
        Assert.True(json.TryGetProperty("color", out var color), "The answer carries no color property.");
        Assert.False(string.IsNullOrWhiteSpace(color.GetString()));
    }
}
