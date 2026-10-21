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
}
