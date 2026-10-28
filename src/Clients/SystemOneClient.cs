using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// Choice, yes/no and score questions answered by a System One model, local or in Ollama's cloud, over /v1/systemone.
/// </summary>
public sealed class SystemOneClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    : TypedHttpClientBase(httpClient), ISystemOneClient
{
    private const string SystemOneEndpoint = "v1/systemone";

    private readonly OllamaOptions _options = options.Value;

    /// <inheritdoc />
    /// <exception cref="HttpBuilderException">
    /// Thrown on a non-success status: 400 for an invalid request, an unsupported model or a prompt that
    /// overflows the context or a cloud model's token limit; 401 for a cloud model without a sign-in or with an
    /// invalid API key; 402 for a cloud model the plan does not include; 404 for a missing model; 413 for local
    /// text over 64 KiB, a request over 32 MiB or a cloud request over 13 MiB; 500 when loading or scoring fails.
    /// </exception>
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires first.</exception>
    public async Task<SystemOneResponse> AnswerAsync(
        SystemOneRequest request,
        CancellationToken cancellationToken = default) =>
        await Post(SystemOneEndpoint)
            .AsJson(request, Wire.SystemOneRequest)
            .SendWithinAsync(Wire.SystemOneResponse, _options, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new InvalidOperationException("Failed to deserialize the System One response.");
}
