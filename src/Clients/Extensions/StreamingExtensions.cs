using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Snail.Toolkit.AI.Ollama.Abstractions;

namespace Snail.Toolkit.AI.Ollama.Clients.Extensions;

/// <summary>
/// Buffering helpers that coalesce token-sized chunks into UI-friendly segments.
/// </summary>
public static class StreamingExtensions
{
    /// <summary>
    /// Buffers stream output into segments of at least <paramref name="chunkSize"/> characters;
    /// null buffers the entire response into a single string.
    /// </summary>
    public static async IAsyncEnumerable<string> ExecuteBufferedAsync<TRequest>(
        this IStreamingAiClient<TRequest> client,
        TRequest request,
        int? chunkSize = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var buffer = new StringBuilder();

        await foreach (var chunk in client.StreamAsync(request, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(chunk.Content)) continue;

            buffer.Append(chunk.Content);

            if (chunkSize.HasValue && buffer.Length >= chunkSize.Value)
            {
                yield return buffer.ToString();
                buffer.Clear();
            }
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }

    /// <summary>
    /// Same buffering for MEAI streaming updates. Only textual content is buffered —
    /// reasoning, tool calls and usage flow past untouched.
    /// </summary>
    public static async IAsyncEnumerable<string> BufferTextAsync(
        this IAsyncEnumerable<ChatResponseUpdate> updates,
        int? chunkSize = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var buffer = new StringBuilder();

        await foreach (var update in updates.WithCancellation(ct).ConfigureAwait(false))
        {
            if (string.IsNullOrEmpty(update.Text)) continue;

            buffer.Append(update.Text);

            if (chunkSize.HasValue && buffer.Length >= chunkSize.Value)
            {
                yield return buffer.ToString();
                buffer.Clear();
            }
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }
}
