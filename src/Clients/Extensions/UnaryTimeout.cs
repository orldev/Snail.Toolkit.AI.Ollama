using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Clients.Extensions;

/// <summary>
/// Bounds a unary call by <see cref="OllamaOptions.Timeout"/>.
/// </summary>
internal static class UnaryTimeout
{
    /// <summary>
    /// Runs the call under a token that fires at the configured timeout or with the caller's token.
    /// </summary>
    /// <exception cref="TimeoutException">The configured timeout expired before Ollama answered.</exception>
    /// <remarks>
    /// The HttpClient itself is unbounded so that streams are never cut short, which leaves the limit to a
    /// linked token. Its expiry would otherwise surface as the same cancellation a caller sees after
    /// cancelling on purpose, and a retry policy cannot tell a slow model from an abandoned request.
    /// </remarks>
    public static async Task<TResult> WithinTimeoutAsync<TResult>(
        this OllamaOptions options,
        Func<CancellationToken, Task<TResult>> call,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        try
        {
            return await call(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException canceled)
            when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Ollama did not answer within {options.Timeout}; raise OllamaOptions.Timeout or stream the call.",
                canceled);
        }
    }
}
