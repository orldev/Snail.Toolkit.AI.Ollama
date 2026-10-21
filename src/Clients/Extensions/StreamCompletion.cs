using System.Runtime.CompilerServices;
using System.Text.Json;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;

namespace Snail.Toolkit.AI.Ollama.Clients.Extensions;

/// <summary>
/// Reads an Ollama NDJSON stream up to its final chunk and refuses to pass a broken one off as complete.
/// </summary>
internal static class StreamCompletion
{
    /// <summary>
    /// Yields chunks until the one marked done; throws on an error line, a line that is not JSON or an end without it.
    /// </summary>
    /// <exception cref="InvalidOperationException">Ollama reported a failure inside the stream.</exception>
    /// <exception cref="IOException">The stream carried a line that is not JSON, or ended before its final chunk.</exception>
    /// <remarks>
    /// Once the 200 headers are out, Ollama can only report a failure as an {"error": ...} line, and a proxy
    /// or a crashed runner can close the stream cleanly mid-answer. Either way the enumeration would end
    /// normally and hand the caller a truncated answer with no finish reason and no usage. A line that is not
    /// JSON — a proxy's error page, a cut line — is the same broken stream, not a contract of its own.
    /// </remarks>
    public static async IAsyncEnumerable<TChunk> UntilDoneAsync<TChunk>(
        this IAsyncEnumerable<TChunk> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TChunk : ResponseBase
    {
        var reading = chunks.WithCancellation(cancellationToken).ConfigureAwait(false).GetAsyncEnumerator();

        await using (reading)
        {
            while (await HasNextAsync(reading).ConfigureAwait(false))
            {
                var chunk = reading.Current;

                if (chunk.Error is { } error)
                {
                    throw new InvalidOperationException($"Ollama failed mid-stream: {error}");
                }

                yield return chunk;

                if (chunk.IsDone)
                {
                    yield break;
                }
            }
        }

        throw new IOException("The Ollama stream ended before its final chunk; the answer is incomplete.");
    }

    /// <exception cref="IOException">The next line is not JSON.</exception>
    private static async ValueTask<bool> HasNextAsync<TChunk>(ConfiguredCancelableAsyncEnumerable<TChunk>.Enumerator reading)
    {
        try
        {
            return await reading.MoveNextAsync();
        }
        catch (JsonException broken)
        {
            throw new IOException("The Ollama stream carried a line that is not JSON; the answer is incomplete.", broken);
        }
    }
}
