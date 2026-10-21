using System.Collections;
using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// The retry budgets of idempotent calls follow OllamaOptions.Timeout instead of contradicting it.
/// </summary>
public class RetryBudgetTests
{
    private sealed class FlakyHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref _calls);
            var status = statuses[Math.Min(call, statuses.Length) - 1];

            Bodies.Add(await request.Content.ReadAsSentAsync(cancellationToken));

            return new HttpResponseMessage(status)
            {
                Content = new StringContent("""{"model":"e","embeddings":[[0.1]]}""", Encoding.UTF8, "application/json")
            };
        }
    }

    /// <summary>A sequence that can be read once, like the rows of a reader or the items of a channel.</summary>
    private sealed class ReadOnce(params string[] items) : IEnumerable<string>
    {
        private int _reads;

        public IEnumerator<string> GetEnumerator() =>
            Interlocked.Increment(ref _reads) == 1 ? ((IEnumerable<string>)items).GetEnumerator() : Enumerable.Empty<string>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static ServiceProvider Build(TimeSpan timeout, HttpMessageHandler? transport = null)
    {
        var services = new ServiceCollection().AddOllama(options =>
        {
            options.DefaultModel = "e";
            options.Timeout = timeout;
        });

        if (transport is not null)
        {
            services.AddHttpClient<IEmbeddingsClient, EmbeddingsClient>().ConfigurePrimaryHttpMessageHandler(() => transport);
        }

        return services.BuildServiceProvider();
    }

    private static HttpStandardResilienceOptions ResilienceOf(ServiceProvider provider) =>
        provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>().Get("IEmbeddingsClient-standard");

    [Fact]
    public void AddOllama_TenMinuteTimeout_EveryBudgetFollowsIt()
    {
        using var provider = Build(TimeSpan.FromMinutes(10));

        var resilience = ResilienceOf(provider);

        Assert.Equal(TimeSpan.FromMinutes(10), resilience.TotalRequestTimeout.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(10), resilience.AttemptTimeout.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(20), resilience.CircuitBreaker.SamplingDuration);
    }

    /// <summary>
    /// Polly validates its timeouts; an infinite Timeout must still build a pipeline that answers.
    /// </summary>
    [Fact]
    public async Task AddOllama_InfiniteTimeout_StillServesCalls()
    {
        var transport = new FlakyHandler(HttpStatusCode.OK);
        await using var provider = Build(Timeout.InfiniteTimeSpan, transport);

        var embeddings = await provider.GetRequiredService<IEmbeddingsClient>().GenerateAsync(["one"]);

        Assert.Single(embeddings);
        Assert.Equal(TimeSpan.FromHours(12), ResilienceOf(provider).TotalRequestTimeout.Timeout);
    }

    [Fact]
    public async Task Embeddings_TransientFailureThenSuccess_RetriesWithinTheBudget()
    {
        var transport = new FlakyHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        await using var provider = Build(TimeSpan.FromSeconds(30), transport);

        var embeddings = await provider.GetRequiredService<IEmbeddingsClient>().GenerateAsync(["one"]);

        Assert.Single(embeddings);
        Assert.Equal(2, transport.Calls);
    }

    /// <summary>
    /// A retry serializes the request again, so the input is enumerated once per attempt: a sequence that reads
    /// once reached the retry empty, and Ollama's 200 with no vectors came back as the answer to the original texts.
    /// </summary>
    [Fact]
    public async Task Embeddings_RetriedAfter503_ResendsTheSameInput()
    {
        var transport = new FlakyHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        await using var provider = Build(TimeSpan.FromSeconds(30), transport);

        await provider.GetRequiredService<IEmbeddingsClient>().GenerateAsync(new ReadOnce("first", "second"));

        Assert.Equal(2, transport.Bodies.Count);
        Assert.Equal(transport.Bodies[0], transport.Bodies[1]);
        Assert.Contains("\"input\":[\"first\",\"second\"]", transport.Bodies[1], StringComparison.Ordinal);
    }
}
