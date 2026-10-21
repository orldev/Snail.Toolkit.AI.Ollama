namespace Snail.Toolkit.AI.Ollama.Clients.Extensions;

/// <summary>
/// The time one unary call may take, retries included, as both its timers read it.
/// </summary>
/// <remarks>
/// The unary timeout and the retry pipeline each held the same setting: the pipeline capped it at 12 hours,
/// while the unary timeout handed it to CancelAfter as it was, which refuses anything past ~49.7 days — so
/// TimeSpan.MaxValue, the usual "no limit", failed every unary call before a byte was sent. One cap read by
/// both keeps them in step: no answer is worth waiting more than 12 hours for, and the earlier of two equal
/// timers is the unary one, so the caller sees a TimeoutException either way.
/// </remarks>
internal static class CallBudget
{
    /// <summary>The longest any unary call waits.</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromHours(12);

    /// <summary>
    /// The configured timeout, with no limit and anything past <see cref="Longest"/> held at it.
    /// </summary>
    public static TimeSpan Of(TimeSpan timeout) =>
        timeout == Timeout.InfiniteTimeSpan || timeout > Longest ? Longest : timeout;
}
