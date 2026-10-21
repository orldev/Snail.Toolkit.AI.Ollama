namespace Snail.Toolkit.AI.Ollama.Clients.Extensions;

/// <summary>
/// The last failed attempt of the unary call running on this flow, so a timeout that ends a run of retries can name
/// its cause.
/// </summary>
/// <remarks>
/// The retry pipeline waits between attempts, and a budget that runs out during the wait cancels it: the caller saw
/// a bare cancellation and the advice to raise the timeout, while every attempt had failed with connection refused.
/// The pipeline records each failure it retries; the unary timeout reads the last one when its budget runs out.
/// </remarks>
internal sealed class FailedAttempts : IDisposable
{
    private static readonly AsyncLocal<FailedAttempts?> Current = new();

    private readonly FailedAttempts? _outer;

    private FailedAttempts()
    {
        _outer = Current.Value;
        Current.Value = this;
    }

    /// <summary>The failure of the latest retried attempt, or null when no attempt has failed.</summary>
    public Exception? Last { get; private set; }

    /// <summary>Starts recording the failed attempts of the call that follows on this flow.</summary>
    public static FailedAttempts Watch() => new();

    /// <summary>Records a failed attempt of the call being watched; does nothing outside one.</summary>
    public static void Record(Exception failure)
    {
        if (Current.Value is { } watching)
        {
            watching.Last = failure;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Current.Value = _outer;
}
