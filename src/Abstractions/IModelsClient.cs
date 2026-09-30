using Snail.Toolkit.AI.Ollama.Contracts.Responses;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Abstractions;

/// <summary>
/// What the server is and holds: its version, the models on disk and in memory, and what each can do.
/// </summary>
public interface IModelsClient
{
    /// <summary>
    /// The server's version, e.g. "0.35.0" or a pre-release such as "0.35.1-rc0".
    /// </summary>
    /// <remarks>A string, not a System.Version: pre-release suffixes would not parse.</remarks>
    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The models pulled to the server's disk.
    /// </summary>
    Task<IReadOnlyList<LocalModel>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The models loaded in memory right now.
    /// </summary>
    Task<IReadOnlyList<RunningModel>> ListRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Capabilities, build details and prompt template of one model; null when the server does not have it.
    /// </summary>
    Task<ModelDescription?> ShowAsync(string model, CancellationToken cancellationToken = default);
}
