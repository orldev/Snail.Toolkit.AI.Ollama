using Snail.Toolkit.AI.Ollama.Contracts.Responses;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Mapping;

/// <summary>
/// Maps /api/generate payloads to the chunks consumers read.
/// </summary>
internal static class GenerateMappingExtensions
{
    /// <summary>
    /// Keeps reasoning on every chunk and the stop reason and usage on the final one.
    /// </summary>
    public static StreamChunk ToStreamChunk(this GenerateResponse response) => new(
        Model: response.Model,
        Content: response.Response,
        IsDone: response.Done,
        Thinking: string.IsNullOrEmpty(response.Thinking) ? null : response.Thinking,
        DoneReason: response.Done ? response.DoneReason : null,
        Usage: response.Done ? response.ToUsageDetails() : null,
        LogProbs: response.LogProbs is { Count: > 0 } logProbs ? logProbs : null);
}
