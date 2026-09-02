using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Mapping;
using Snail.Toolkit.HttpBuilder.Extensions;

using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatResponse = Snail.Toolkit.AI.Ollama.Contracts.Responses.ChatResponse;
using IChatClient = Snail.Toolkit.AI.Ollama.Abstractions.IChatClient;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// MEAI chat client over Ollama's native /api/chat: NDJSON streaming, tool calling,
/// thinking and images.
/// </summary>
public class ChatClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    : TypedHttpClientBase(httpClient), IChatClient
{
    private readonly OllamaOptions _options = options.Value;

    private readonly ChatClientMetadata _metadata = new(
        providerName: "ollama",
        providerUri: Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out var uri) ? uri : null,
        defaultModelId: options.Value.DefaultModel);

    private const string ChatEndpoint = "/api/chat";

    /// <exception cref="InvalidOperationException">Thrown when no model is configured anywhere.</exception>
    private string ResolveModel(ChatOptions? options) =>
        options?.ModelId ?? _options.DefaultModel
        ?? throw new InvalidOperationException(
            "No model specified: set ChatOptions.ModelId or OllamaOptions.DefaultModel.");

    /// <summary>
    /// Bounds a unary call by OllamaOptions.Timeout — the HttpClient itself is unbounded
    /// so that streams are never cut short.
    /// </summary>
    private CancellationTokenSource CreateUnaryTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);

        return timeout;
    }

    /// <inheritdoc />
    public async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.ToInternalRequest(options, ResolveModel(options));
        request.Stream = false;

        using var timeout = CreateUnaryTimeout(cancellationToken);

        var response = await Post(ChatEndpoint)
                           .AsJson(request)
                           .SendAsync<ChatResponse>(timeout.Token)
                           .ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Failed to deserialize the chat response.");

        return response.ToAiResponse();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = messages.ToInternalRequest(options, ResolveModel(options));

        await foreach (var chunk in Post(ChatEndpoint)
                           .AsJson(request)
                           .SendAsNdjsonAsync<ChatResponse>(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return chunk.ToAiUpdate();

            if (chunk.Done)
                yield break;
        }
    }

    /// <summary>
    /// Serves <see cref="ChatClientMetadata"/> for MEAI telemetry wrappers, or the client itself.
    /// </summary>
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is not null ? null
        : serviceType == typeof(ChatClientMetadata) ? _metadata
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    /// <summary>
    /// Releases nothing: the transport belongs to <see cref="IHttpClientFactory"/>, and MEAI
    /// pipeline wrappers cascade Dispose — disposing the shared HttpClient would break other consumers.
    /// </summary>
    public void Dispose() => GC.SuppressFinalize(this);
}
