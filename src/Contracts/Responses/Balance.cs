using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// The usage credits left on the account, as /api/balance reports them.
/// </summary>
/// <param name="Included">What the plan includes.</param>
/// <param name="Purchased">What was bought on top.</param>
public sealed record Balance(
    [property: JsonPropertyName("included")] IncludedCredits Included,
    [property: JsonPropertyName("purchased")] PurchasedCredits Purchased);

/// <summary>
/// The credits a plan includes: in USD for a current plan, as session and weekly limits for a legacy one.
/// </summary>
/// <remarks>
/// Ollama answers with one shape or the other and names neither, so both are read into one record: a current plan
/// fills <see cref="BalanceUsd"/>, <see cref="AllowanceUsd"/> and <see cref="Period"/>, a legacy plan
/// <see cref="Session"/> and <see cref="Weekly"/>, and the rest stays null.
/// </remarks>
public sealed record IncludedCredits
{
    /// <summary>The included credits left in USD; null on a legacy plan.</summary>
    [JsonPropertyName("balance_usd")]
    public double? BalanceUsd { get; init; }

    /// <summary>The included credits for the whole period in USD; null on a legacy plan.</summary>
    [JsonPropertyName("allowance_usd")]
    public double? AllowanceUsd { get; init; }

    /// <summary>The period the allowance covers; null on a legacy plan.</summary>
    [JsonPropertyName("period")]
    public CreditPeriod? Period { get; init; }

    /// <summary>The limit of the current session on a legacy plan; null on a current one.</summary>
    [JsonPropertyName("session")]
    public PlanLimit? Session { get; init; }

    /// <summary>The weekly limit on a legacy plan; null on a current one.</summary>
    [JsonPropertyName("weekly")]
    public PlanLimit? Weekly { get; init; }
}

/// <summary>
/// The stretch of time included credits are granted for.
/// </summary>
/// <param name="From">The start in UTC, inclusive.</param>
/// <param name="Until">The end in UTC, exclusive.</param>
public sealed record CreditPeriod(
    [property: JsonPropertyName("from")] DateTimeOffset From,
    [property: JsonPropertyName("until")] DateTimeOffset Until);

/// <summary>
/// A limit of a legacy plan: how much of it is left and when it resets.
/// </summary>
/// <param name="RemainingPercent">The share of the limit left, from 0 to 100.</param>
/// <param name="ResetsAt">When the limit resets, in UTC.</param>
public sealed record PlanLimit(
    [property: JsonPropertyName("remaining_percent")] double RemainingPercent,
    [property: JsonPropertyName("resets_at")] DateTimeOffset ResetsAt);

/// <summary>
/// The purchased credits that have not expired.
/// </summary>
/// <param name="BalanceUsd">What is left of them in USD.</param>
public sealed record PurchasedCredits([property: JsonPropertyName("balance_usd")] double BalanceUsd);
