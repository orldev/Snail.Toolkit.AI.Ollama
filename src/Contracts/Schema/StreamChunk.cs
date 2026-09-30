using Microsoft.Extensions.AI;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// A flattened streaming chunk handed to consumers of the generate stream.
/// </summary>
/// <param name="Model">The model that produced the chunk.</param>
/// <param name="Content">The answer text in this chunk; empty while the model is still reasoning.</param>
/// <param name="IsDone">True on the final chunk only.</param>
/// <param name="Thinking">The reasoning text in this chunk, when thinking was requested.</param>
/// <param name="DoneReason">Why generation stopped — "stop", "length" — on the final chunk only.</param>
/// <param name="Usage">Prompt, cached and generated token counts, on the final chunk only.</param>
/// <param name="LogProbs">Log probabilities of this chunk's tokens, when logprobs was requested.</param>
public record StreamChunk(
    string Model,
    string Content,
    bool IsDone,
    string? Thinking = null,
    string? DoneReason = null,
    UsageDetails? Usage = null,
    IReadOnlyList<LogProbResult>? LogProbs = null);
