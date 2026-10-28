using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// Cloud usage over a stretch of time, as /api/usage reports it: totals and a bucket per hour or day.
/// </summary>
public sealed record UsageReport
{
    /// <summary>The stretch reported: "24h", "7d" or "30d".</summary>
    [JsonPropertyName("range")]
    public string Range { get; init; } = string.Empty;

    /// <summary>Whose requests are counted: "self" or "team".</summary>
    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;

    /// <summary>The size of a bucket: "hour" for a day, "day" for a week or a month.</summary>
    [JsonPropertyName("granularity")]
    public string Granularity { get; init; } = string.Empty;

    /// <summary>"model" when the report is broken down by model; null otherwise.</summary>
    [JsonPropertyName("group_by")]
    public string? GroupBy { get; init; }

    /// <summary>The start of the stretch in UTC, inclusive.</summary>
    [JsonPropertyName("from")]
    public DateTimeOffset From { get; init; }

    /// <summary>The end of the stretch in UTC, exclusive.</summary>
    [JsonPropertyName("until")]
    public DateTimeOffset Until { get; init; }

    /// <summary>The usage across the whole stretch, the groups beyond the ten heaviest included.</summary>
    [JsonPropertyName("totals")]
    public UsageSummary Totals { get; init; } = new();

    /// <summary>The usage hour by hour or day by day, in order, empty buckets included.</summary>
    [JsonPropertyName("buckets")]
    public IReadOnlyList<UsageBucket> Buckets { get; init; } = [];
}

/// <summary>
/// What a set of cloud requests cost and consumed.
/// </summary>
public record UsageMetrics
{
    /// <summary>How many requests were recorded.</summary>
    [JsonPropertyName("request_count")]
    public int RequestCount { get; init; }

    /// <summary>
    /// The value of the requests in USD, what the plan covers and what purchased credits paid alike; null for a range
    /// that holds legacy requests.
    /// </summary>
    [JsonPropertyName("usage_usd")]
    public double? UsageUsd { get; init; }

    /// <summary>The input tokens, cached ones included.</summary>
    [JsonPropertyName("input_tokens")]
    public long? InputTokens { get; init; }

    /// <summary>The input tokens read from cache, already counted in <see cref="InputTokens"/>.</summary>
    [JsonPropertyName("cached_input_tokens")]
    public long? CachedInputTokens { get; init; }

    /// <summary>The output tokens.</summary>
    [JsonPropertyName("output_tokens")]
    public long? OutputTokens { get; init; }
}

/// <summary>
/// The usage of one model.
/// </summary>
public sealed record UsageGroup : UsageMetrics
{
    /// <summary>The model's name.</summary>
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;
}

/// <summary>
/// Usage in total and, when the report is broken down by model, model by model.
/// </summary>
public record UsageSummary : UsageMetrics
{
    /// <summary>
    /// Up to ten models, the heaviest across the whole stretch, in the same order in every bucket; a bucket leaves out
    /// those it holds no request of. Null when the report is not broken down by model.
    /// </summary>
    [JsonPropertyName("groups")]
    public IReadOnlyList<UsageGroup>? Groups { get; init; }

    /// <summary>The models beyond the ten, together; null when there are none.</summary>
    [JsonPropertyName("remainder")]
    public UsageMetrics? Remainder { get; init; }
}

/// <summary>
/// The usage of one hour or one day.
/// </summary>
public sealed record UsageBucket : UsageSummary
{
    /// <summary>The start of the bucket in UTC, inclusive.</summary>
    [JsonPropertyName("from")]
    public DateTimeOffset From { get; init; }

    /// <summary>The end of the bucket in UTC, exclusive.</summary>
    [JsonPropertyName("until")]
    public DateTimeOffset Until { get; init; }

    /// <summary>Whether the bucket is the current hour or day, still in progress.</summary>
    [JsonPropertyName("partial")]
    public bool IsPartial { get; init; }
}
