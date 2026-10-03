using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Marks a fact that only runs against a live Ollama 0.35.0+ holding a System One model: OLLAMA_URL points
/// at the server and OLLAMA_SYSTEMONE_MODEL names the model, e.g. "nimble".
/// </summary>
public sealed class SystemOneFactAttribute : FactAttribute
{
    public SystemOneFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_URL"))
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_SYSTEMONE_MODEL")))
        {
            Skip = "Set OLLAMA_URL and OLLAMA_SYSTEMONE_MODEL to run the System One integration test.";
        }
    }
}

/// <summary>
/// Marks a fact that only runs against a live Ollama 0.35.1+ holding a vision decision model: OLLAMA_URL
/// points at the server and OLLAMA_SYSTEMONE_VISION_MODEL names the model, e.g. "clef-flash".
/// </summary>
public sealed class SystemOneVisionFactAttribute : FactAttribute
{
    public SystemOneVisionFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_URL"))
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_SYSTEMONE_VISION_MODEL")))
        {
            Skip = "Set OLLAMA_URL and OLLAMA_SYSTEMONE_VISION_MODEL to run the System One vision test.";
        }
    }
}

/// <summary>
/// Live verification that every question kind survives the round trip to a real System One model.
/// </summary>
public class SystemOneLiveTests
{
    private static SystemOneClient CreateClient()
    {
        string baseUrl = Environment.GetEnvironmentVariable("OLLAMA_URL")!;

        return new SystemOneClient(
            new HttpClient { BaseAddress = new Uri(baseUrl) },
            Options.Create(new OllamaOptions { BaseUrl = baseUrl, Timeout = TimeSpan.FromMinutes(5) }));
    }

    /// <summary>
    /// A flat blue square leaves one right answer, so the test checks that the image reached the model rather
    /// than how well the model sees.
    /// </summary>
    [SystemOneVisionFact]
    public async Task AnswerAsync_ImageAttached_ScoresTheImage()
    {
        var response = await CreateClient().AnswerAsync(new SystemOneRequest(
            Environment.GetEnvironmentVariable("OLLAMA_SYSTEMONE_VISION_MODEL")!,
            "The user attached a picture.",
            new Dictionary<string, SystemOneQuestion>
            {
                ["color"] = new ChoiceQuestion("Which colour fills the attached image?", new Dictionary<string, string?>
                {
                    ["red"] = null,
                    ["green"] = null,
                    ["blue"] = null
                })
            })
        {
            Images = [Convert.ToBase64String(SolidPng.Of(64, 64, red: 0, green: 0, blue: 255))]
        });

        Assert.Equal("blue", Assert.IsType<ChoiceAnswer>(response.Answers["color"]).Choice);
    }

    [SystemOneFact]
    public async Task AnswerAsync_EveryQuestionKind_ComesBackAsItsOwnAnswer()
    {
        string baseUrl = Environment.GetEnvironmentVariable("OLLAMA_URL")!;
        var client = new SystemOneClient(
            new HttpClient { BaseAddress = new Uri(baseUrl) },
            Options.Create(new OllamaOptions { BaseUrl = baseUrl, Timeout = TimeSpan.FromMinutes(5) }));

        var response = await client.AnswerAsync(new SystemOneRequest(
            Environment.GetEnvironmentVariable("OLLAMA_SYSTEMONE_MODEL")!,
            "Our checkout has returned 500 errors since 9am.",
            new Dictionary<string, SystemOneQuestion>
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

        var label = Assert.IsType<ChoiceAnswer>(response.Answers["label"]);
        var urgent = Assert.IsType<NoulAnswer>(response.Answers["urgent"]);
        var severity = Assert.IsType<ScoreAnswer>(response.Answers["severity"]);
        Assert.Equal("bug", label.Choice);
        Assert.InRange(urgent.Probability, 0, 1);
        Assert.InRange(severity.Score, 0, 2);
        Assert.Equal(3, severity.Legend.Count);
        Assert.True(response.Usage.InputTokens > 0);
    }
}
