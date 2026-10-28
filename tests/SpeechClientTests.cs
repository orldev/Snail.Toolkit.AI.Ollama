using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Tests for <see cref="SpeechClient"/>: what reaches /v1/audio/speech and how the audio comes back.
/// </summary>
[Experimental("MEAI001")]
public class SpeechClientTests
{
    private sealed class StubHandler(byte[] audio, string mediaType = "audio/wav", HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.AbsolutePath;
            RequestBody = await request.Content.ReadAsSentAsync(cancellationToken);

            var content = new ByteArrayContent(audio);
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

            return new HttpResponseMessage(status) { Content = content };
        }
    }

    private static readonly byte[] Wave = [0x52, 0x49, 0x46, 0x46, 0xFF, 0xFF, 0xFF, 0xFF, 0x57, 0x41, 0x56, 0x45];

    private static SpeechClient CreateClient(StubHandler handler, string? defaultModel = "phonon") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") },
            Options.Create(new OllamaOptions { DefaultModel = defaultModel }));

    [Fact]
    public async Task GetAudioAsync_NoOptions_SendsOnlyTheModelAndTheText()
    {
        var handler = new StubHandler(Wave);

        await CreateClient(handler).GetAudioAsync("Hello from Ollama.");

        Assert.Equal("/v1/audio/speech", handler.RequestPath);
        Assert.Equal("""{"model":"phonon","input":"Hello from Ollama."}""", handler.RequestBody);
    }

    /// <summary>
    /// MEAI takes a format as a name or as a media type; Ollama takes the name, so "audio/pcm" must arrive as "pcm".
    /// </summary>
    [Fact]
    public async Task GetAudioAsync_VoiceFormatAndSpeed_ReachOllamaUnderItsOwnNames()
    {
        var handler = new StubHandler(Wave, "audio/pcm");

        await CreateClient(handler, defaultModel: null).GetAudioAsync("Hi", new TextToSpeechOptions
        {
            ModelId = "phonon",
            VoiceId = "Zoey",
            AudioFormat = "audio/pcm",
            Speed = 1.5f
        });

        Assert.Equal("""{"model":"phonon","input":"Hi","voice":"Zoey","response_format":"pcm","speed":1.5}""", handler.RequestBody);
    }

    [Fact]
    public async Task GetAudioAsync_Answered_ReturnsTheAudioWithItsMediaType()
    {
        var response = await CreateClient(new StubHandler(Wave, "audio/pcm")).GetAudioAsync("Hi");

        var audio = Assert.IsType<DataContent>(Assert.Single(response.Contents));
        Assert.Equal("audio/pcm", audio.MediaType);
        Assert.Equal(Wave, audio.Data.ToArray());
        Assert.Equal("phonon", response.ModelId);
    }

    /// <summary>
    /// Updates carry the audio as it arrives; one after another they must make the whole file, byte for byte.
    /// </summary>
    [Fact]
    public async Task GetStreamingAudioAsync_LongAudio_UpdatesJoinIntoTheWholeAudio()
    {
        byte[] audio = [.. Enumerable.Range(0, 50_000).Select(index => (byte)(index % 251))];

        List<byte> joined = [];
        int updates = 0;

        await foreach (var update in CreateClient(new StubHandler(audio)).GetStreamingAudioAsync("A long story."))
        {
            var chunk = Assert.IsType<DataContent>(Assert.Single(update.Contents));
            Assert.Equal("audio/wav", chunk.MediaType);
            Assert.Equal(TextToSpeechResponseUpdateKind.AudioUpdating, update.Kind);
            joined.AddRange(chunk.Data.ToArray());
            updates++;
        }

        Assert.Equal(audio, joined);
        Assert.True(updates > 1, "the audio came as one update, not as a stream");
    }

    /// <summary>
    /// The endpoint has no field for pitch, volume or language; sent anyway, they would be dropped without a word.
    /// </summary>
    [Theory]
    [InlineData("pitch")]
    [InlineData("volume")]
    [InlineData("language")]
    public async Task GetAudioAsync_OptionOllamaHasNoFieldFor_ThrowsBeforeSending(string option)
    {
        var handler = new StubHandler(Wave);
        var options = option switch
        {
            "pitch" => new TextToSpeechOptions { Pitch = 1.2f },
            "volume" => new TextToSpeechOptions { Volume = 0.5f },
            _ => new TextToSpeechOptions { Language = "en-US" }
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => CreateClient(handler).GetAudioAsync("Hi", options));

        Assert.Null(handler.RequestPath);
    }

    [Fact]
    public async Task GetAudioAsync_UnknownVoice_ThrowsWithOllamasReason()
    {
        var refusal = Encoding.UTF8.GetBytes("""{"error":{"message":"voice \"nobody\" not found; available voices: Zoey, default","type":"invalid_request_error"}}""");
        var handler = new StubHandler(refusal, "application/json", HttpStatusCode.BadRequest);

        var failure = await Assert.ThrowsAsync<HttpBuilderException>(
            () => CreateClient(handler).GetAudioAsync("Hi", new TextToSpeechOptions { VoiceId = "nobody" }));

        Assert.Equal(HttpStatusCode.BadRequest, failure.StatusCode);
        Assert.Contains("available voices", failure.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStreamingAudioAsync_Refused_ThrowsBeforeTheFirstUpdate()
    {
        var refusal = Encoding.UTF8.GetBytes("""{"error":{"message":"only speed 1 is supported"}}""");
        var handler = new StubHandler(refusal, "application/json", HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<HttpBuilderException>(async () =>
        {
            await foreach (var _ in CreateClient(handler).GetStreamingAudioAsync("Hi", new TextToSpeechOptions { Speed = 2 })) { }
        });
    }

    [Fact]
    public async Task GetAudioAsync_NoModelAnywhere_ThrowsBeforeSending()
    {
        var handler = new StubHandler(Wave);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateClient(handler, defaultModel: null).GetAudioAsync("Hi"));

        Assert.Null(handler.RequestPath);
    }

    [Fact]
    public void AddOllamaSpeech_Registered_ResolvesTheSpeechClientWithItsMetadata()
    {
        using var provider = new ServiceCollection()
            .AddOllama(options => options.DefaultModel = "phonon")
            .AddOllamaSpeech()
            .BuildServiceProvider();

        var speech = provider.GetRequiredService<ITextToSpeechClient>();

        Assert.IsType<SpeechClient>(speech);
        Assert.Equal("ollama", speech.GetService<TextToSpeechClientMetadata>()!.ProviderName);
        Assert.Equal("phonon", speech.GetService<TextToSpeechClientMetadata>()!.DefaultModelId);
    }
}
