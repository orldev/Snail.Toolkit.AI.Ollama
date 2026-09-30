using System.Runtime.CompilerServices;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Contracts.Mapping;
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
    private const string GenerateEndpoint = "api/generate";

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when Ollama reports a failure inside the stream.</exception>
    /// <exception cref="IOException">Thrown when the stream ends before its final chunk.</exception>
    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        GenerateRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chunks = Post(GenerateEndpoint)
            .AsJson(request, Wire.Json)
            .SendAsNdjsonAsync<GenerateResponse>(cancellationToken)
            .UntilDoneAsync(cancellationToken);

        await foreach (var chunk in chunks.ConfigureAwait(false))
        {
            yield return chunk.ToStreamChunk();
        }
    }
}
