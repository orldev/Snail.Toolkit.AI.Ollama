using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Responses;

/// <summary>
/// Shared response surface: completion state, token counters and timings (nanoseconds).
/// </summary>
internal abstract record ResponseBase
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("done")]
    public bool Done { get; init; }

    /// <summary>
    /// Why generation stopped, e.g. "stop" or "length". Present only on the final chunk.
    /// </summary>
    [JsonPropertyName("done_reason")]
    public string? DoneReason { get; init; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; init; }

    [JsonPropertyName("load_duration")]
    public long? LoadDuration { get; init; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; init; }

    [JsonPropertyName("prompt_eval_duration")]
    public long? PromptEvalDuration { get; init; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; init; }

    [JsonPropertyName("eval_duration")]
    public long? EvalDuration { get; init; }

    [JsonPropertyName("logprobs")]
    public IEnumerable<LogProbResult>? LogProbs { get; init; }
}
