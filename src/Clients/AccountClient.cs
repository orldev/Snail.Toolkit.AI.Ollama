using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// The ollama.com account's cloud usage and credits over /api/usage and /api/balance.
/// </summary>
public sealed class AccountClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    : TypedHttpClientBase(httpClient), IAccountClient
{
    private const string UsageEndpoint = "api/usage";

    private const string BalanceEndpoint = "api/balance";

    private readonly OllamaOptions _options = options.Value;

    /// <inheritdoc />
    /// <exception cref="HttpBuilderException">
    /// Thrown on a non-success status: 401 without a sign-in or with an invalid key, 403 for a suspended account or a
    /// team report without team admin, 429 past the rate limit once the retries ran out, 503 while reporting is down.
    /// </exception>
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires first.</exception>
    public async Task<UsageReport> GetUsageAsync(UsageQuery? query = null, CancellationToken cancellationToken = default)
    {
        var wanted = query ?? new UsageQuery();

        var request = Get(UsageEndpoint)
            .Query("range", ToRange(wanted.Range))
            .Query("scope", ToScope(wanted.Scope));

        if (wanted.IsGroupedByModel)
        {
            request = request.Query("group_by", "model");
        }

        var report = await request.SendWithinAsync(Wire.UsageReport, _options, cancellationToken).ConfigureAwait(false);

        return report ?? throw new InvalidOperationException("Ollama answered /api/usage without a report.");
    }

    /// <inheritdoc />
    /// <exception cref="HttpBuilderException">
    /// Thrown on a non-success status: 401 without a sign-in or with an invalid key, 403 for a suspended account,
    /// 429 past the rate limit once the retries ran out, 503 while reporting is down.
    /// </exception>
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires first.</exception>
    public async Task<Balance> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        var balance = await Get(BalanceEndpoint)
            .SendWithinAsync(Wire.Balance, _options, cancellationToken)
            .ConfigureAwait(false);

        return balance ?? throw new InvalidOperationException("Ollama answered /api/balance without a balance.");
    }

    private static string ToRange(UsageRange range) => range switch
    {
        UsageRange.Day => "24h",
        UsageRange.Month => "30d",
        _ => "7d"
    };

    private static string ToScope(UsageScope scope) => scope is UsageScope.Team ? "team" : "self";
}
