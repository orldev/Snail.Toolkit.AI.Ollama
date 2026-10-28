namespace Snail.Toolkit.AI.Ollama.Contracts.Requests;

/// <summary>
/// Which stretch of cloud usage /api/usage reports, for whom and how it is broken down.
/// </summary>
public sealed record UsageQuery
{
    /// <summary>The completed hours or days to include, plus the current partial one; a week by default.</summary>
    public UsageRange Range { get; init; } = UsageRange.Week;

    /// <summary>Your own requests, or your team's — which needs a team admin; your own by default.</summary>
    public UsageScope Scope { get; init; } = UsageScope.Self;

    /// <summary>Whether totals and buckets are broken down by model, the ten heaviest each.</summary>
    public bool IsGroupedByModel { get; init; }
}

/// <summary>
/// The stretch of time a usage report covers; a day is broken into hours, a week and a month into days.
/// </summary>
public enum UsageRange
{
    /// <summary>The last 24 hours, hour by hour ("24h").</summary>
    Day,

    /// <summary>The last 7 days, day by day ("7d").</summary>
    Week,

    /// <summary>The last 30 days, day by day ("30d").</summary>
    Month
}

/// <summary>
/// Whose requests a usage report counts.
/// </summary>
public enum UsageScope
{
    /// <summary>Your own requests.</summary>
    Self,

    /// <summary>Your team's requests; needs a team admin, otherwise Ollama answers 403.</summary>
    Team
}
