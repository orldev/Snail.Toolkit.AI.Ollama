using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Tests for <see cref="SystemOneClient"/>: the wire shape of questions and the kinds of answers read back.
/// </summary>
public class SystemOneClientTests
{
    private const string ChoiceResponse = """
        {"model":"nimble","answers":{"label":{"type":"choice","choice":"bug",
        "probabilities":{"billing":0.0125,"bug":0.9781,"account":0.0093},"confidence":0.8906}},
        "usage":{"input_tokens":174,"output_tokens":1}}
        """;

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }

        public JsonElement RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.AbsolutePath;
            RequestBody = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private static SystemOneClient CreateClient(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions()));

    private static SystemOneRequest Ask(string name, SystemOneQuestion question) =>
        new("nimble", "Our checkout has returned 500 errors since 9am.", new Dictionary<string, SystemOneQuestion>
        {
            [name] = question
        });

    [Fact]
    public async Task AnswerAsync_ChoiceQuestion_PostsTypedCriteriaToSystemOne()
    {
        var handler = new StubHandler(ChoiceResponse);
        var client = CreateClient(handler);

        await client.AnswerAsync(Ask("label", new ChoiceQuestion(
            "Which label fits this ticket?",
            new Dictionary<string, string?> { ["billing"] = "Payments and refunds", ["bug"] = null })));

        var label = handler.RequestBody.GetProperty("questions").GetProperty("label");
        Assert.Equal("/v1/systemone", handler.RequestPath);
        Assert.Equal("choice", label.GetProperty("type").GetString());
        Assert.Equal("Payments and refunds", label.GetProperty("criteria").GetProperty("billing").GetString());
        Assert.Equal(JsonValueKind.Null, label.GetProperty("criteria").GetProperty("bug").ValueKind);
    }

    /// <summary>
    /// The endpoint refuses generation controls, so the fields every other request carries must stay off the wire.
    /// </summary>
    [Fact]
    public async Task AnswerAsync_AnyRequest_SendsNoGenerationControls()
    {
        var handler = new StubHandler(ChoiceResponse);
        var client = CreateClient(handler);

        await client.AnswerAsync(Ask("urgent", new NoulQuestion("Is this urgent?")));

        Assert.False(handler.RequestBody.TryGetProperty("stream", out _));
        Assert.False(handler.RequestBody.TryGetProperty("options", out _));
        Assert.False(handler.RequestBody.TryGetProperty("keep_alive", out _));
        Assert.False(handler.RequestBody.TryGetProperty("images", out _));
    }

    [Fact]
    public async Task AnswerAsync_Images_SentTopLevelInOrder()
    {
        var handler = new StubHandler(ChoiceResponse);
        var client = CreateClient(handler);

        await client.AnswerAsync(Ask("has_logo", new NoulQuestion("Does the image contain a logo?")) with
        {
            Images = ["iVBORw0KGgo=", "UklGRg=="]
        });

        var images = handler.RequestBody.GetProperty("images");
        Assert.Equal(["iVBORw0KGgo=", "UklGRg=="], images.EnumerateArray().Select(image => image.GetString()));
        Assert.False(handler.RequestBody.GetProperty("questions").GetProperty("has_logo").TryGetProperty("images", out _));
    }

    [Fact]
    public async Task AnswerAsync_NoulWithoutCriteria_LeavesOllamaDefaults()
    {
        var handler = new StubHandler(ChoiceResponse);
        var client = CreateClient(handler);

        await client.AnswerAsync(Ask("urgent", new NoulQuestion("Is this urgent?")));

        var urgent = handler.RequestBody.GetProperty("questions").GetProperty("urgent");
        Assert.Equal("noul", urgent.GetProperty("type").GetString());
        Assert.False(urgent.TryGetProperty("criteria", out _));
    }

    [Fact]
    public async Task AnswerAsync_NoulCriteriaReworded_SendsThemAsFalseAndTrue()
    {
        var handler = new StubHandler(ChoiceResponse);
        var client = CreateClient(handler);

        await client.AnswerAsync(Ask("urgent", new NoulQuestion("Is this urgent?", new NoulCriteria(Yes: "Page on-call"))));

        var criteria = handler.RequestBody.GetProperty("questions").GetProperty("urgent").GetProperty("criteria");
        Assert.Equal("Page on-call", criteria.GetProperty("true").GetString());
        Assert.False(criteria.TryGetProperty("false", out _));
    }

    [Fact]
    public async Task AnswerAsync_ScoreQuestion_SendsCriteriaInOrder()
    {
        var handler = new StubHandler(ChoiceResponse);
        var client = CreateClient(handler);

        await client.AnswerAsync(Ask("severity", new ScoreQuestion("How severe?", ["minor", "major", "outage"])));

        var criteria = handler.RequestBody.GetProperty("questions").GetProperty("severity").GetProperty("criteria");
        Assert.Equal(["minor", "major", "outage"], criteria.EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task AnswerAsync_ChoiceResponse_ReadsPickDistributionAndUsage()
    {
        var client = CreateClient(new StubHandler(ChoiceResponse));

        var response = await client.AnswerAsync(Ask("label", new NoulQuestion("unused")));

        var label = Assert.IsType<ChoiceAnswer>(response.Answers["label"]);
        Assert.Equal("bug", label.Choice);
        Assert.Equal(0.9781, label.Probabilities["bug"]);
        Assert.Equal(0.8906, label.Confidence);
        Assert.Equal(new SystemOneUsage(174, 1), response.Usage);
    }

    [Fact]
    public async Task AnswerAsync_ScoreResponse_ReadsIndexedLegendAndProbabilities()
    {
        var client = CreateClient(new StubHandler("""
            {"model":"nimble","answers":{"severity":{"type":"score","score":1.5,
            "legend":{"0":"minor","1":"major","2":"outage"},"probabilities":{"0":0.25,"1":0.5,"2":0.25},
            "confidence":0.6}},"usage":{"input_tokens":90,"output_tokens":1}}
            """));

        var response = await client.AnswerAsync(Ask("severity", new NoulQuestion("unused")));

        var severity = Assert.IsType<ScoreAnswer>(response.Answers["severity"]);
        Assert.Equal(1.5, severity.Score);
        Assert.Equal("outage", severity.Legend[2]);
        Assert.Equal(0.5, severity.Probabilities[1]);
    }

    /// <summary>
    /// System.Text.Json reads a discriminator only in first position unless told otherwise, and Ollama's
    /// contract does not promise that order.
    /// </summary>
    [Fact]
    public async Task AnswerAsync_TypeWrittenLast_StillReadsTheAnswerKind()
    {
        var client = CreateClient(new StubHandler("""
            {"model":"nimble","answers":{"urgent":{"noul":0.75,"type":"noul"}},
            "usage":{"input_tokens":40,"output_tokens":1}}
            """));

        var response = await client.AnswerAsync(Ask("urgent", new NoulQuestion("Is this urgent?")));

        Assert.Equal(0.75, Assert.IsType<NoulAnswer>(response.Answers["urgent"]).Probability);
    }
}
