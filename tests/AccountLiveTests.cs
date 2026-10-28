using System.Net;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Live verification of the account reports through a real server: a local Ollama 0.40.1+ passes them on to
/// ollama.com when signed in and refuses them with 401 when not; either is an answer, a body that does not read is not.
/// </summary>
public class AccountLiveTests
{
    private static AccountClient CreateClient()
    {
        string baseUrl = Environment.GetEnvironmentVariable("OLLAMA_URL")!;

        return new AccountClient(
            new HttpClient { BaseAddress = new Uri(baseUrl) },
            Options.Create(new OllamaOptions { BaseUrl = baseUrl }));
    }

    [OllamaFact]
    public async Task GetBalanceAsync_LiveServer_AnswersWithCreditsOrRefusesWithoutSignIn()
    {
        try
        {
            var balance = await CreateClient().GetBalanceAsync();

            Assert.True(balance.Included.BalanceUsd is not null || balance.Included.Session is not null, "the included credits read as neither shape");
        }
        catch (HttpBuilderException refused) when (refused.StatusCode == HttpStatusCode.Unauthorized)
        {
            Assert.Contains("credentials", refused.Body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [OllamaFact]
    public async Task GetUsageAsync_LiveServer_AnswersWithAReportOrRefusesWithoutSignIn()
    {
        try
        {
            var report = await CreateClient().GetUsageAsync(new UsageQuery { Range = UsageRange.Day });

            Assert.Equal("hour", report.Granularity);
        }
        catch (HttpBuilderException refused) when (refused.StatusCode == HttpStatusCode.Unauthorized)
        {
            Assert.Contains("credentials", refused.Body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
