using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// Shared response surface: completion state, token counters and timings (nanoseconds).
/// </summary>
internal abstract record ResponseBase
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("done")]
    public bool IsDone { get; set; }

    /// <summary>
    /// Why generation stopped, e.g. "stop" or "length". Present only on the final chunk.
    /// </summary>
    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; set; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }

    [JsonPropertyName("load_duration")]
    public long? LoadDuration { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }

    /// <summary>
    /// Prompt tokens served from the KV cache instead of being evaluated again.
    /// </summary>
    [JsonPropertyName("prompt_eval_cached_count")]
    public int? PromptEvalCachedCount { get; set; }

    [JsonPropertyName("prompt_eval_duration")]
    public long? PromptEvalDuration { get; set; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }

    [JsonPropertyName("eval_duration")]
    public long? EvalDuration { get; set; }

    [JsonPropertyName("logprobs")]
    public IReadOnlyList<LogProbResult>? LogProbs { get; set; }

    /// <summary>
    /// A failure Ollama hit after the 200 headers were sent, reported as a line of the stream itself.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
