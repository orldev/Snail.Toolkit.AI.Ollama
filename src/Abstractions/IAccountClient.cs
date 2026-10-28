using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;

namespace Snail.Toolkit.AI.Ollama.Abstractions;

/// <summary>
/// What the ollama.com account has spent on cloud models, web search and web fetch, and what credits it has left.
/// </summary>
/// <remarks>
/// Answered by ollama.com — with BaseUrl https://ollama.com and an ApiKey, or through a local Ollama 0.40.1+ signed in
/// to ollama.com, which passes the calls on. Without a sign-in or with an invalid key Ollama answers 401.
/// </remarks>
public interface IAccountClient
{
    /// <summary>
    /// The cloud usage of a week by default, in totals and a bucket per day.
    /// </summary>
    Task<UsageReport> GetUsageAsync(UsageQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The included and purchased credits left.
    /// </summary>
    Task<Balance> GetBalanceAsync(CancellationToken cancellationToken = default);
}
