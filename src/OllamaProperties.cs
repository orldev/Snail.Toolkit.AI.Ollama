namespace Snail.Toolkit.AI.Ollama;

/// <summary>
/// The keys of the Ollama settings MEAI has no typed option for, as read from AdditionalProperties.
/// </summary>
/// <remarks>
/// A key is matched exactly, so a misspelt one — "numctx", "keepalive" — was ignored without a word and the call ran
/// on the server's defaults; a constant turns the misspelling into a compile error. The values are Ollama's own field
/// names and never change, so compiling them into the caller is safe.
/// </remarks>
public static class OllamaProperties
{
    /// <summary>
    /// Chat: whether and how hard the model thinks — a boolean, or a level the model accepts ("low", "medium",
    /// "high", "max"); wins over ChatOptions.Reasoning.
    /// </summary>
    public const string Think = "think";

    /// <summary>
    /// Chat and embeddings: how long the model stays loaded after the call — a duration string such as "5m", "0"
    /// to unload at once or "-1" to keep it; unset leaves the server's OLLAMA_KEEP_ALIVE.
    /// </summary>
    public const string KeepAlive = "keep_alive";

    /// <summary>
    /// Chat: the context window in tokens, an integer; images consume it fast.
    /// </summary>
    public const string NumCtx = "num_ctx";

    /// <summary>
    /// Chat: the minimum probability of a token relative to the likeliest one, a float between 0 and 1.
    /// </summary>
    public const string MinP = "min_p";

    /// <summary>
    /// Chat: a boolean asking for the log probability of every generated token; the answer carries them under the
    /// same key, as an IReadOnlyList of LogProbResult.
    /// </summary>
    public const string LogProbs = "logprobs";

    /// <summary>
    /// Chat: how many likeliest alternatives to report per token, an integer from 0 to 20; implies
    /// <see cref="LogProbs"/>.
    /// </summary>
    public const string TopLogProbs = "top_logprobs";

    /// <summary>
    /// Embeddings: a boolean; false makes an input longer than the window fail with 400 instead of being cut.
    /// </summary>
    public const string Truncate = "truncate";
}
