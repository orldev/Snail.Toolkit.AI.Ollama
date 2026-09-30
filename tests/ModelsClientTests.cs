using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Tests for <see cref="ModelsClient"/>: payloads taken from Ollama 0.35.0 and how a missing model reads.
/// </summary>
public class ModelsClientTests
{
    private sealed class RoutingHandler(IReadOnlyDictionary<string, (HttpStatusCode Status, string Body)> routes)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var (status, body) = routes[request.RequestUri!.AbsolutePath];

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static ModelsClient CreateClient(string path, string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new RoutingHandler(new Dictionary<string, (HttpStatusCode, string)> { [path] = (status, body) }))
            {
                BaseAddress = new Uri("http://localhost:11434")
            },
            Options.Create(new OllamaOptions()));

    [Fact]
    public async Task GetVersionAsync_Answered_ReturnsVersionString()
    {
        var client = CreateClient("/api/version", """{"version":"0.35.1-rc0"}""");

        Assert.Equal("0.35.1-rc0", await client.GetVersionAsync());
    }

    [Fact]
    public async Task ListAsync_Tags_ReadsDetailsAndCapabilities()
    {
        var client = CreateClient("/api/tags", """
            {"models":[{"name":"nimble:latest","model":"nimble:latest","modified_at":"2026-09-30T08:39:21.238720245-04:00",
            "size":9527502277,"digest":"24e5","details":{"parent_model":"","format":"gguf","family":"qwen35",
            "families":["qwen35"],"parameter_size":"9.0B","quantization_level":"Q8_0","context_length":262144,
            "embedding_length":4096},"capabilities":["decision","tools","thinking","completion"]}]}
            """);

        var model = Assert.Single(await client.ListAsync());

        Assert.Equal("nimble:latest", model.Name);
        Assert.Equal(9527502277, model.Size);
        Assert.Equal(262144, model.Details!.ContextLength);
        Assert.Contains("decision", model.Capabilities!);
    }

    [Fact]
    public async Task ListAsync_NoModelsPulled_ReturnsEmpty()
    {
        var client = CreateClient("/api/tags", """{"models":[]}""");

        Assert.Empty(await client.ListAsync());
    }

    [Fact]
    public async Task ListRunningAsync_Ps_ReadsMemoryAndExpiry()
    {
        var client = CreateClient("/api/ps", """
            {"models":[{"name":"gemma3:1b","model":"gemma3:1b","size":998800097,"digest":"8648",
            "details":{"format":"gguf","family":"gemma3"},"expires_at":"2026-09-30T09:07:54.361958-04:00",
            "size_vram":998800097,"context_length":32768}]}
            """);

        var running = Assert.Single(await client.ListRunningAsync());

        Assert.Equal(running.Size, running.SizeVram);
        Assert.Equal(32768, running.ContextLength);
    }

    [Fact]
    public async Task ShowAsync_Model_ReadsCapabilitiesThinkingAndContextLength()
    {
        var client = CreateClient("/api/show", """
            {"capabilities":["completion","vision","tools","thinking"],"details":{"family":"qwen35moe"},
             "model_info":{"general.architecture":"qwen35moe","qwen35moe.context_length":262144},
             "thinking":{"values":[false,true],"default":true}}
            """);

        var description = await client.ShowAsync("qwen");

        Assert.True(description!.Supports("tools"));
        Assert.False(description.Supports("decision"));
        Assert.Equal(262144, description.ContextLength);
        Assert.Equal(2, description.Thinking!.Values.Count);
    }

    /// <summary>
    /// Capabilities are advisory: an entry that is not a name is skipped, and the rest of the description still reads.
    /// </summary>
    [Fact]
    public async Task ShowAsync_CapabilityThatIsNotAName_SkipsItAndReadsTheRest()
    {
        var client = CreateClient("/api/show", """
            {"capabilities":["completion",7,"tools",null,{"kind":"vision"}],
             "model_info":{"general.architecture":"qwen35moe","qwen35moe.context_length":262144}}
            """);

        var description = await client.ShowAsync("qwen");

        Assert.Equal(["completion", "tools"], description!.Capabilities);
        Assert.Equal(262144, description.ContextLength);
    }

    /// <summary>
    /// Capabilities that are not a list say nothing about the model; they do not cost the caller its window and template.
    /// </summary>
    [Fact]
    public async Task ShowAsync_CapabilitiesNotAList_ReadsThemAsUnknown()
    {
        var client = CreateClient("/api/show", """
            {"capabilities":"tools","template":"{{ .Prompt }}",
             "model_info":{"general.architecture":"gemma3","gemma3.context_length":32768}}
            """);

        var description = await client.ShowAsync("gemma3");

        Assert.Null(description!.Capabilities);
        Assert.False(description.Supports("tools"));
        Assert.Equal(32768, description.ContextLength);
        Assert.Equal("{{ .Prompt }}", description.Template);
    }

    /// <summary>
    /// Ollama writes model_info with sorted keys, so a projector's window can come first; the architecture names the model's own.
    /// </summary>
    [Fact]
    public async Task ShowAsync_SeveralContextLengths_ReadsTheArchitecturesOwn()
    {
        var client = CreateClient("/api/show", """
            {"model_info":{"clip.context_length":4096,"general.architecture":"qwen25vl","qwen25vl.context_length":128000}}
            """);

        Assert.Equal(128000, (await client.ShowAsync("qwen2.5vl"))!.ContextLength);
    }

    /// <summary>
    /// An architecture whose own key is missing still has a window somewhere; the model reads as unfamiliar, not blind.
    /// </summary>
    [Fact]
    public async Task ShowAsync_ArchitectureWithoutItsOwnKey_ReadsAnyContextLength()
    {
        var client = CreateClient("/api/show", """
            {"model_info":{"general.architecture":"something-new","something-else.context_length":16384}}
            """);

        Assert.Equal(16384, (await client.ShowAsync("new"))!.ContextLength);
    }

    /// <summary>
    /// A model the server does not have is an ordinary answer to "what is this model", not a failure.
    /// </summary>
    [Fact]
    public async Task ShowAsync_MissingModel_ReturnsNull()
    {
        var client = CreateClient("/api/show", """{"error":"model 'nope:latest' not found"}""", HttpStatusCode.NotFound);

        Assert.Null(await client.ShowAsync("nope"));
    }

    [Fact]
    public void AddOllama_Registered_FacadeExposesModels()
    {
        using var provider = new ServiceCollection().AddOllama().BuildServiceProvider();

        Assert.IsType<ModelsClient>(provider.GetRequiredService<IOllamaClient>().Models);
    }
}
