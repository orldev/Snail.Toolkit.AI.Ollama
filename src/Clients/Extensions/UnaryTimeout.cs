using System.Text.Json.Serialization.Metadata;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Clients.Extensions;

/// <summary>
/// Bounds a unary call by <see cref="OllamaOptions.Timeout"/>.
/// </summary>
internal static class UnaryTimeout
{
    /// <summary>
    /// Sends the request and reads its JSON answer within the configured timeout.
    /// </summary>
    /// <exception cref="TimeoutException">The configured timeout expired before Ollama answered.</exception>
    public static Task<TAnswer?> SendWithinAsync<TAnswer>(
        this IHttpRequestBuilder request,
        JsonTypeInfo<TAnswer> answer,
        OllamaOptions options,
        CancellationToken cancellationToken) =>
        options.WithinTimeoutAsync(token => request.SendAsync(answer, token), cancellationToken);

    /// <summary>
    /// Sends the request and reads its whole body as bytes, with the media type it came with, within the configured
    /// timeout.
    /// </summary>
    /// <exception cref="TimeoutException">The configured timeout expired before the whole body arrived.</exception>
    public static Task<(byte[] Body, string? MediaType)> ReadWithinAsync(
        this IHttpRequestBuilder request,
        OllamaOptions options,
        CancellationToken cancellationToken) =>
        options.WithinTimeoutAsync(token => ReadAsync(request, token), cancellationToken);

    private static async Task<(byte[] Body, string? MediaType)> ReadAsync(IHttpRequestBuilder request, CancellationToken cancellationToken)
    {
        using var response = await request
            .SendCheckedAsync(HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        return (body, response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Runs the call under a token that fires at the configured timeout or with the caller's token.
    /// </summary>
    /// <exception cref="TimeoutException">The configured timeout expired before Ollama answered.</exception>
    /// <remarks>
    /// The HttpClient itself is unbounded so that streams are never cut short, which leaves the limit to a
    /// linked token. Its expiry would otherwise surface as the same cancellation a caller sees after
    /// cancelling on purpose, and a retry policy cannot tell a slow model from an abandoned request.
    /// </remarks>
    private static async Task<TResult> WithinTimeoutAsync<TResult>(
        this OllamaOptions options,
        Func<CancellationToken, Task<TResult>> call,
        CancellationToken cancellationToken)
    {
        TimeSpan budget = CallBudget.Of(options.Timeout);

        using var attempts = FailedAttempts.Watch();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(budget);

        try
        {
            return await call(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException canceled)
            when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw attempts.Last is { } cause
                ? new TimeoutException(
                    $"Ollama did not answer within {budget}: every attempt failed, the last with \"{cause.Message}\"; check that Ollama is running and reachable.",
                    cause)
                : new TimeoutException(
                    $"Ollama did not answer within {budget}; raise OllamaOptions.Timeout or stream the call.",
                    canceled);
        }
    }
}
