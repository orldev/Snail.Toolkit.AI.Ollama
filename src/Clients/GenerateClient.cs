using System.Runtime.CompilerServices;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// Single-shot text generation over /api/generate — no conversation history.
/// </summary>
public class GenerateClient(HttpClient httpClient)
    : TypedHttpClientBase(httpClient), IGenerateClient
{
    private const string GenerateEndpoint = "/api/generate";

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        GenerateRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in Post(GenerateEndpoint)
                           .AsJson(request)
                           .SendAsNdjsonAsync<GenerateResponse>(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return new StreamChunk(chunk.Model, chunk.Response, chunk.Done);

            if (chunk.Done)
                yield break;
        }
    }
}
