using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// The settings a configuration section names reach <see cref="OllamaOptions"/>: none of them is left at
/// its default, which is what a binder that binds nothing would leave.
/// </summary>
public class OptionsBindingTests
{
    [Fact]
    public void AddOllama_ConfigurationSection_BindsEverySetting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:BaseUrl"] = "http://gpu-box:11434",
                ["Ollama:ApiKey"] = "key",
                ["Ollama:Timeout"] = "00:05:00",
                ["Ollama:DefaultModel"] = "qwen3"
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddOllama(configuration.GetSection("Ollama"))
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OllamaOptions>>().Value;

        Assert.Equal("http://gpu-box:11434", options.BaseUrl);
        Assert.Equal("key", options.ApiKey);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Timeout);
        Assert.Equal("qwen3", options.DefaultModel);
    }

    /// <summary>
    /// A BaseUrl without a scheme parses as a URI whose scheme is the host — "localhost:11434" — and failed only at
    /// the first call, far from the setting; an empty one failed with a bare UriFormatException.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("localhost:11434")]
    [InlineData("/api")]
    [InlineData("ftp://gpu-box:11434")]
    public void AddOllama_BaseUrlNotHttp_FailsNamingTheSetting(string baseUrl)
    {
        using var provider = new ServiceCollection()
            .AddOllama(options => options.BaseUrl = baseUrl)
            .BuildServiceProvider();

        var refusal = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OllamaOptions>>().Value);

        Assert.Contains(nameof(OllamaOptions.BaseUrl), refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A zero timeout failed every unary call at once, and a negative one threw from inside the call.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddOllama_TimeoutNotPositive_FailsNamingTheSetting(int seconds)
    {
        using var provider = new ServiceCollection()
            .AddOllama(options => options.Timeout = TimeSpan.FromSeconds(seconds))
            .BuildServiceProvider();

        var refusal = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<OllamaOptions>>().Value);

        Assert.Contains(nameof(OllamaOptions.Timeout), refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOllama_InfiniteTimeoutAndPathPrefix_AreValid()
    {
        using var provider = new ServiceCollection()
            .AddOllama(options =>
            {
                options.BaseUrl = "https://gateway.example/ollama";
                options.Timeout = Timeout.InfiniteTimeSpan;
            })
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OllamaOptions>>().Value;

        Assert.Equal(Timeout.InfiniteTimeSpan, options.Timeout);
    }

    /// <summary>
    /// A host validates the settings as it starts, not at the first call. The retry pipelines read the same settings,
    /// so the host gathers their refusals too, into one AggregateException.
    /// </summary>
    [Fact]
    public void AddOllama_InvalidSettings_AreValidatedOnStart()
    {
        var services = new ServiceCollection().AddOllama(options => options.BaseUrl = "localhost:11434");

        using var provider = services.BuildServiceProvider();

        var refusal = Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains($"OllamaOptions.{nameof(OllamaOptions.BaseUrl)}", refusal.Message, StringComparison.Ordinal);
    }
}
