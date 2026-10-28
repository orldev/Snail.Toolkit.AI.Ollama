using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// MEAI text-to-speech over Ollama's /v1/audio/speech: a speech model such as phonon reads text aloud, whole or as a
/// stream that starts playing before the text is spoken.
/// </summary>
/// <remarks>
/// Experimental because MEAI's <see cref="ITextToSpeechClient"/> is (MEAI001). Requires Ollama 0.40.9 or later; speech
/// runs on macOS for now. Ollama answers 400 for what the model cannot honour — a speed other than 1 for phonon, a
/// format other than wav or pcm, a voice it does not have, listing the ones it has — rather than ignoring it.
/// </remarks>
[Experimental("MEAI001")]
public sealed class SpeechClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    : TypedHttpClientBase(httpClient), ITextToSpeechClient
{
    private const string SpeechEndpoint = "v1/audio/speech";

    private const string DefaultMediaType = "audio/wav";

    private const int ChunkSize = 16384;

    private readonly OllamaOptions _options = options.Value;

    private readonly TextToSpeechClientMetadata _metadata = new(
        providerName: "ollama",
        providerUri: Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out var uri) ? uri : null,
        defaultModelId: options.Value.DefaultModel);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when no model is configured anywhere.</exception>
    /// <exception cref="NotSupportedException">Thrown for a pitch, a volume or a language, which Ollama has no field for.</exception>
    /// <exception cref="HttpBuilderException">Thrown when Ollama refuses the request, with its reason in the body.</exception>
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires before the whole audio arrives.</exception>
    public async Task<TextToSpeechResponse> GetAudioAsync(
        string text, TextToSpeechOptions? options = null, CancellationToken cancellationToken = default)
    {
        var request = ToRequest(text, options);

        var (audio, mediaType) = await Post(SpeechEndpoint)
            .AsJson(request, Wire.SpeechRequest)
            .ReadWithinAsync(_options, cancellationToken)
            .ConfigureAwait(false);

        return new TextToSpeechResponse([new DataContent(audio, mediaType ?? DefaultMediaType)]) { ModelId = request.Model };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Every update carries the audio that arrived since the last one; written one after another they make the whole
    /// file. A streamed WAV header is written before the length is known, so its size fields hold the maximum value.
    /// The stream runs until the audio ends or the caller's token fires; OllamaOptions.Timeout does not bound it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when no model is configured anywhere.</exception>
    /// <exception cref="NotSupportedException">Thrown for a pitch, a volume or a language, which Ollama has no field for.</exception>
    /// <exception cref="HttpBuilderException">Thrown when Ollama refuses the request, with its reason in the body.</exception>
    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(
        string text, TextToSpeechOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = ToRequest(text, options);

        using var response = await Post(SpeechEndpoint)
            .AsJson(request, Wire.SpeechRequest)
            .SendCheckedAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        string mediaType = response.Content.Headers.ContentType?.MediaType ?? DefaultMediaType;
        var audio = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] buffer = new byte[ChunkSize];

        await using (audio.ConfigureAwait(false))
        {
            int read;

            while ((read = await audio.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                yield return new TextToSpeechResponseUpdate([new DataContent(buffer.AsMemory(0, read).ToArray(), mediaType)])
                {
                    Kind = TextToSpeechResponseUpdateKind.AudioUpdating,
                    ModelId = request.Model
                };
            }
        }
    }

    /// <summary>
    /// Serves <see cref="TextToSpeechClientMetadata"/> for MEAI wrappers, or the client itself.
    /// </summary>
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is not null ? null
        : serviceType == typeof(TextToSpeechClientMetadata) ? _metadata
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    /// <summary>
    /// Releases nothing: the transport belongs to <see cref="IHttpClientFactory"/>.
    /// </summary>
    public void Dispose()
    {
    }

    /// <exception cref="InvalidOperationException">No model is configured anywhere.</exception>
    /// <exception cref="NotSupportedException">A pitch, a volume or a language is asked for.</exception>
    private SpeechRequest ToRequest(string text, TextToSpeechOptions? options)
    {
        RefuseUnsupported(options);

        string model = options?.ModelId ?? _options.DefaultModel
            ?? throw new InvalidOperationException("No model specified: set TextToSpeechOptions.ModelId or OllamaOptions.DefaultModel.");

        return new SpeechRequest(model, text)
        {
            Voice = options?.VoiceId,
            ResponseFormat = ToResponseFormat(options?.AudioFormat),
            Speed = options?.Speed
        };
    }

    /// <summary>
    /// The OpenAI-compatible endpoint has no field for pitch, volume or language, so asking for one would be ignored.
    /// </summary>
    /// <exception cref="NotSupportedException">A pitch, a volume or a language is asked for.</exception>
    private static void RefuseUnsupported(TextToSpeechOptions? options)
    {
        if (options is { Pitch: not null } or { Volume: not null } or { Language: not null })
        {
            throw new NotSupportedException(
                "Ollama's speech endpoint takes a voice, a format and a speed only; pitch, volume and language cannot reach the model.");
        }
    }

    /// <summary>
    /// "wav" and "pcm" pass as they are; a media type such as "audio/wav" is read as its subtype.
    /// </summary>
    private static string? ToResponseFormat(string? audioFormat) =>
        audioFormat is not null && audioFormat.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            ? audioFormat["audio/".Length..]
            : audioFormat;
}
