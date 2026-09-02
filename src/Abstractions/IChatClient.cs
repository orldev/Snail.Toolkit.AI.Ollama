namespace Snail.Toolkit.AI.Ollama.Abstractions;

/// <summary>
/// The chat surface is the MEAI contract itself, so the client composes with MEAI
/// pipelines (function invocation, telemetry, caching) directly.
/// </summary>
public interface IChatClient : Microsoft.Extensions.AI.IChatClient;
