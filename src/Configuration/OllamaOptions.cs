namespace Snail.Toolkit.AI.Ollama.Configuration;

/// <summary>
/// Connection and behavior settings shared by all clients.
/// </summary>
public class OllamaOptions
{
    /// <summary>
    /// Defaults to the local Ollama endpoint; point it at https://ollama.com to use
    /// Ollama Cloud (an <see cref="ApiKey"/> is required there).
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Sent as a Bearer token when set; null (the default) sends no Authorization
    /// header — a local Ollama needs none.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Bounds unary (non-streaming) requests only. Streams run until the model finishes
    /// or the caller's <see cref="CancellationToken"/> fires.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Null (the default) means every call must name its model explicitly — the client
    /// throws a clear exception instead of sending an empty model id.
    /// </summary>
    public string? DefaultModel { get; set; }
}
