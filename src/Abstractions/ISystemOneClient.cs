using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;

namespace Snail.Toolkit.AI.Ollama.Abstractions;

/// <summary>
/// Classification, yes/no and scoring by a local System One model: probabilities instead of generated text.
/// </summary>
public interface ISystemOneClient
{
    /// <summary>
    /// Scores every question against the request's state in one call.
    /// </summary>
    /// <remarks>
    /// The endpoint never streams and returns one JSON document, so the call is bounded by
    /// OllamaOptions.Timeout like any other unary request.
    /// </remarks>
    Task<SystemOneResponse> AnswerAsync(SystemOneRequest request, CancellationToken cancellationToken = default);
}
