using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Marks a fact that only runs against a live Ollama 0.40.9+ holding a speech model: OLLAMA_URL points at the server
/// and OLLAMA_SPEECH_MODEL names the model, e.g. "phonon".
/// </summary>
public sealed class SpeechFactAttribute : FactAttribute
{
    public SpeechFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_URL"))
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_SPEECH_MODEL")))
        {
            Skip = "Set OLLAMA_URL and OLLAMA_SPEECH_MODEL to run the speech integration tests.";
        }
    }
}

/// <summary>
/// Live verification that a speech model reads text aloud through the MEAI text-to-speech surface.
/// </summary>
[Experimental("MEAI001")]
public class SpeechLiveTests
{
    private static string BaseUrl => Environment.GetEnvironmentVariable("OLLAMA_URL")!;

    private static string Model => Environment.GetEnvironmentVariable("OLLAMA_SPEECH_MODEL")!;

    private static SpeechClient CreateClient() =>
        new(new HttpClient { BaseAddress = new Uri(BaseUrl) },
            Options.Create(new OllamaOptions { BaseUrl = BaseUrl, DefaultModel = Model, Timeout = TimeSpan.FromMinutes(5) }));

    private static bool IsWave(byte[] audio) =>
        audio.Length > 44
        && Encoding.ASCII.GetString(audio, 0, 4) == "RIFF"
        && Encoding.ASCII.GetString(audio, 8, 4) == "WAVE";

    [SpeechFact]
    public async Task GetAudioAsync_Text_ReturnsAWaveFile()
    {
        var response = await CreateClient().GetAudioAsync("Hello from Ollama.");

        var audio = Assert.IsType<DataContent>(Assert.Single(response.Contents));
        Assert.Equal("audio/wav", audio.MediaType);
        Assert.True(IsWave(audio.Data.ToArray()), "the audio is not a WAV file");
    }

    [SpeechFact]
    public async Task GetAudioAsync_Pcm_ReturnsRawSamples()
    {
        var response = await CreateClient().GetAudioAsync("Hello.", new TextToSpeechOptions { AudioFormat = "pcm" });

        var audio = Assert.IsType<DataContent>(Assert.Single(response.Contents));
        Assert.Equal("audio/pcm", audio.MediaType);
        Assert.False(IsWave(audio.Data.ToArray()));
        Assert.Equal(0, audio.Data.Length % 2);
    }

    /// <summary>
    /// Several sentences take long enough to speak that the audio must arrive in more than one piece.
    /// </summary>
    [SpeechFact]
    public async Task GetStreamingAudioAsync_Sentences_ArriveInPiecesThatJoinIntoAWaveFile()
    {
        List<byte> joined = [];
        int updates = 0;

        await foreach (var update in CreateClient().GetStreamingAudioAsync(
            "Playback starts while the rest is still being generated. This sentence is long enough to need several pieces."))
        {
            joined.AddRange(Assert.IsType<DataContent>(Assert.Single(update.Contents)).Data.ToArray());
            updates++;
        }

        Assert.True(updates > 1, "the audio came as one piece");
        Assert.True(IsWave([.. joined]), "the joined audio is not a WAV file");
    }

    [SpeechFact]
    public async Task GetAudioAsync_UnknownVoice_RefusedWithTheVoicesTheModelHas()
    {
        var failure = await Assert.ThrowsAsync<HttpBuilderException>(
            () => CreateClient().GetAudioAsync("Hi", new TextToSpeechOptions { VoiceId = "snail-toolkit-no-such-voice" }));

        Assert.Equal(HttpStatusCode.BadRequest, failure.StatusCode);
        Assert.Contains("available voices", failure.Body, StringComparison.Ordinal);
    }

    [SpeechFact]
    public async Task ShowAsync_SpeechModel_ListsItsVoices()
    {
        var models = new ModelsClient(new HttpClient { BaseAddress = new Uri(BaseUrl) }, Options.Create(new OllamaOptions { BaseUrl = BaseUrl }));

        var description = await models.ShowAsync(Model);

        Assert.True(description!.Supports("speech"));
        Assert.Contains("default", description.Voices!);
    }
}
