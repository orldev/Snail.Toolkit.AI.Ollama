using System.Text;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Reads a request body the way a socket does.
/// </summary>
/// <remarks>
/// ReadAsStringAsync buffers the content inside the request, so a retry would send the buffer again and a
/// content serialized afresh for each attempt — the real transport's behaviour — would go untested. A retried
/// embedding call sent "input":[] for a sequence that reads once, and only a stub reading this way saw it.
/// </remarks>
internal static class SentBody
{
    public static async Task<string> ReadAsSentAsync(this HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return string.Empty;
        }

        using var wire = new MemoryStream();
        await content.CopyToAsync(wire, cancellationToken);

        return Encoding.UTF8.GetString(wire.ToArray());
    }
}
