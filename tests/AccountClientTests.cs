using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Tests for <see cref="AccountClient"/>: the queries /api/usage and /api/balance receive and the answers of the
/// Ollama 0.40.12 spec read back.
/// </summary>
public class AccountClientTests
{
    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? RequestPathAndQuery { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPathAndQuery = request.RequestUri?.PathAndQuery;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private const string Report = """
        {"range":"7d","scope":"self","granularity":"day","group_by":"model",
         "from":"2026-10-21T00:00:00Z","until":"2026-10-28T13:00:00Z",
         "totals":{"request_count":42,"usage_usd":1.25,"input_tokens":120000,"cached_input_tokens":30000,"output_tokens":8000,
           "groups":[{"model":"clef:cloud","request_count":40,"usage_usd":1.2,"input_tokens":110000,"cached_input_tokens":30000,"output_tokens":7500}],
           "remainder":{"request_count":2,"usage_usd":0.05}},
         "buckets":[
           {"from":"2026-10-27T00:00:00Z","until":"2026-10-28T00:00:00Z","request_count":30,"usage_usd":0.9,
            "groups":[{"model":"clef:cloud","request_count":30,"usage_usd":0.9}]},
           {"from":"2026-10-28T00:00:00Z","until":"2026-10-29T00:00:00Z","partial":true,"request_count":12,"usage_usd":0.35}]}
        """;

    private static AccountClient CreateClient(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://ollama.com/") }, Options.Create(new OllamaOptions()));

    [Fact]
    public async Task GetUsageAsync_NoQuery_AsksForAWeekOfYourOwnRequests()
    {
        var handler = new StubHandler(Report);

        await CreateClient(handler).GetUsageAsync();

        Assert.Equal("/api/usage?range=7d&scope=self", handler.RequestPathAndQuery);
    }

    [Fact]
    public async Task GetUsageAsync_DayForTheTeamByModel_SendsEachAsOllamaNamesIt()
    {
        var handler = new StubHandler(Report);

        await CreateClient(handler).GetUsageAsync(new UsageQuery { Range = UsageRange.Day, Scope = UsageScope.Team, IsGroupedByModel = true });

        Assert.Equal("/api/usage?range=24h&scope=team&group_by=model", handler.RequestPathAndQuery);
    }

    [Fact]
    public async Task GetUsageAsync_ReportByModel_ReadsTotalsGroupsRemainderAndBuckets()
    {
        var report = await CreateClient(new StubHandler(Report)).GetUsageAsync(new UsageQuery { IsGroupedByModel = true });

        Assert.Equal("day", report.Granularity);
        Assert.Equal("model", report.GroupBy);
        Assert.Equal(42, report.Totals.RequestCount);
        Assert.Equal(30000, report.Totals.CachedInputTokens);
        Assert.Equal("clef:cloud", Assert.Single(report.Totals.Groups!).Model);
        Assert.Equal(0.05, report.Totals.Remainder!.UsageUsd);
        Assert.Equal(2, report.Buckets.Count);
        Assert.False(report.Buckets[0].IsPartial);
        Assert.True(report.Buckets[1].IsPartial);
        Assert.Null(report.Buckets[1].Groups);
    }

    [Fact]
    public async Task GetBalanceAsync_CurrentPlan_ReadsIncludedCreditsInUsd()
    {
        var client = CreateClient(new StubHandler("""
            {"included":{"balance_usd":3.5,"allowance_usd":10,"period":{"from":"2026-10-01T00:00:00Z","until":"2026-11-01T00:00:00Z"}},
             "purchased":{"balance_usd":20}}
            """));

        var balance = await client.GetBalanceAsync();

        Assert.Equal(3.5, balance.Included.BalanceUsd);
        Assert.Equal(10, balance.Included.AllowanceUsd);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), balance.Included.Period!.Until);
        Assert.Null(balance.Included.Session);
        Assert.Equal(20, balance.Purchased.BalanceUsd);
    }

    /// <summary>
    /// A legacy plan answers in another shape that Ollama does not name; read into the same record, it must fill the
    /// limits and leave the dollar fields unknown.
    /// </summary>
    [Fact]
    public async Task GetBalanceAsync_LegacyPlan_ReadsSessionAndWeeklyLimits()
    {
        var client = CreateClient(new StubHandler("""
            {"included":{"session":{"remaining_percent":62.5,"resets_at":"2026-10-28T18:00:00Z"},
                         "weekly":{"remaining_percent":80,"resets_at":"2026-11-02T00:00:00Z"}},
             "purchased":{"balance_usd":0}}
            """));

        var balance = await client.GetBalanceAsync();

        Assert.Equal(62.5, balance.Included.Session!.RemainingPercent);
        Assert.Equal(80, balance.Included.Weekly!.RemainingPercent);
        Assert.Null(balance.Included.BalanceUsd);
        Assert.Null(balance.Included.Period);
    }

    [Fact]
    public async Task GetBalanceAsync_NotSignedIn_ThrowsUnauthorized()
    {
        var client = CreateClient(new StubHandler("""{"error":"invalid credentials"}""", HttpStatusCode.Unauthorized));

        var failure = await Assert.ThrowsAsync<HttpBuilderException>(() => client.GetBalanceAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
    }

    [Fact]
    public void AddOllama_Registered_ResolvesTheAccountClientWithRetries()
    {
        using var provider = new ServiceCollection().AddOllama().BuildServiceProvider();

        Assert.IsType<AccountClient>(provider.GetRequiredService<IAccountClient>());
        Assert.Equal(
            TimeSpan.FromSeconds(100),
            provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>().Get("IAccountClient-standard").TotalRequestTimeout.Timeout);
    }
}
