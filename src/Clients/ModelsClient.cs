using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// The server's version and models over /api/version, /api/tags, /api/ps and /api/show.
/// </summary>
public sealed class ModelsClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    : TypedHttpClientBase(httpClient), IModelsClient
{
    private const string VersionEndpoint = "api/version";

    private const string TagsEndpoint = "api/tags";

    private const string RunningEndpoint = "api/ps";

    private const string ShowEndpoint = "api/show";

    private readonly OllamaOptions _options = options.Value;

    private sealed record VersionReply([property: JsonPropertyName("version")] string Version);

    private sealed record LocalModels([property: JsonPropertyName("models")] IReadOnlyList<LocalModel>? Models);

    private sealed record RunningModels([property: JsonPropertyName("models")] IReadOnlyList<RunningModel>? Models);

    private sealed record ShowRequest([property: JsonPropertyName("model")] string Model);

    /// <inheritdoc />
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires first.</exception>
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _options
            .WithinTimeoutAsync(token => Get(VersionEndpoint).WithJsonOptions(Wire.Json).SendAsync<VersionReply>(token), cancellationToken)
            .ConfigureAwait(false);

        return reply?.Version ?? throw new InvalidOperationException("Ollama answered /api/version without a version.");
    }

    /// <inheritdoc />
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires first.</exception>
    public async Task<IReadOnlyList<LocalModel>> ListAsync(CancellationToken cancellationToken = default)
    {
        var listing = await _options
            .WithinTimeoutAsync(token => Get(TagsEndpoint).WithJsonOptions(Wire.Json).SendAsync<LocalModels>(token), cancellationToken)
            .ConfigureAwait(false);

        return listing?.Models ?? [];
    }

    /// <inheritdoc />
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires first.</exception>
    public async Task<IReadOnlyList<RunningModel>> ListRunningAsync(CancellationToken cancellationToken = default)
    {
        var listing = await _options
            .WithinTimeoutAsync(token => Get(RunningEndpoint).WithJsonOptions(Wire.Json).SendAsync<RunningModels>(token), cancellationToken)
            .ConfigureAwait(false);

        return listing?.Models ?? [];
    }

    /// <inheritdoc />
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires first.</exception>
    /// <remarks>A missing model is an answer, not a failure: Ollama's 404 comes back as null.</remarks>
    public async Task<ModelDescription?> ShowAsync(string model, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _options
                .WithinTimeoutAsync(
                    token => Post(ShowEndpoint).AsJson(new ShowRequest(model), Wire.Json).SendAsync<ModelDescription>(token),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpBuilderException missing) when (missing.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
