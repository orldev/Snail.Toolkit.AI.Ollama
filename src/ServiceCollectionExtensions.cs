using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama;

/// <summary>
/// Registers the Ollama feature: options, typed clients and the facade.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers Ollama clients configured in code.
        /// </summary>
        public IServiceCollection AddOllama(Action<OllamaOptions>? configure = null)
        {
            var options = services.AddOptions<OllamaOptions>();

            if (configure is not null)
            {
                options.Configure(configure);
            }

            return services.AddClients();
        }

        /// <summary>
        /// Registers Ollama clients configured from configuration.
        /// </summary>
        /// <param name="configuration">The section to bind, e.g. config.GetSection("Ollama").</param>
        public IServiceCollection AddOllama(IConfiguration configuration)
        {
            services.Configure<OllamaOptions>(configuration);

            return services.AddClients();
        }

        private IServiceCollection AddClients()
        {
            services.AddHttpClient<IChatClient, ChatClient>(ConfigureTransport)
                .ConfigurePrimaryHttpMessageHandler(CreateStreamingSafeHandler);

            services.AddHttpClient<IGenerateClient, GenerateClient>(ConfigureTransport)
                .ConfigurePrimaryHttpMessageHandler(CreateStreamingSafeHandler);

            services.AddHttpClient<IEmbeddingsClient, EmbeddingsClient>(ConfigureTransport)
                .ConfigurePrimaryHttpMessageHandler(CreateStreamingSafeHandler)
                .AddRetriesForIdempotentCalls();

            services.AddTransient<IOllamaClient, OllamaClient>();

            return services;
        }
    }

    /// <summary>
    /// Leaves HttpClient.Timeout infinite — it would abort reading a streamed body
    /// mid-generation. Time limits live in ConnectTimeout, the per-request
    /// OllamaOptions.Timeout and the caller's token.
    /// </summary>
    /// <remarks>
    /// The base address always ends in a slash and endpoints are relative, so Ollama published under a
    /// proxy prefix such as https://gateway/ollama keeps the prefix instead of losing it to "/api/chat".
    /// </remarks>
    private static void ConfigureTransport(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<OllamaOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : $"{options.BaseUrl}/");
        client.Timeout = Timeout.InfiniteTimeSpan;

        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    }

    /// <summary>
    /// Connection limits that keep long generations alive and pick up DNS changes
    /// of a remote endpoint.
    /// </summary>
    private static SocketsHttpHandler CreateStreamingSafeHandler() => new()
    {
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
    };

    /// <summary>
    /// Embeddings are the only idempotent calls — chat and generation stream, and a retry would
    /// replay a half-consumed generation.
    /// </summary>
    /// <remarks>
    /// OllamaOptions.Timeout is the budget of the whole call, retries included, so every resilience timeout
    /// is derived from it. Fixed budgets contradicted it: a 2-minute attempt under a 100-second call could
    /// never be retried, and a 5-minute total silently capped any Timeout set above it. An attempt may use
    /// the full budget because a slow local model is still working — only a fast failure is worth another
    /// try. The circuit breaker must sample at least two attempts' worth of time.
    /// </remarks>
    private static void AddRetriesForIdempotentCalls(this IHttpClientBuilder builder) =>
        builder.AddStandardResilienceHandler().Configure((resilience, provider) =>
        {
            TimeSpan budget = CallBudget(provider.GetRequiredService<IOptions<OllamaOptions>>().Value.Timeout);

            resilience.TotalRequestTimeout.Timeout = budget;
            resilience.AttemptTimeout.Timeout = budget;
            resilience.CircuitBreaker.SamplingDuration = budget * 2;
        });

    /// <summary>
    /// The configured timeout, held inside the ranges Polly validates: an infinite or oversized timeout
    /// becomes 12 hours, anything shorter than a second becomes one second.
    /// </summary>
    private static TimeSpan CallBudget(TimeSpan timeout)
    {
        TimeSpan longest = TimeSpan.FromHours(12);

        if (timeout == Timeout.InfiniteTimeSpan || timeout > longest)
        {
            return longest;
        }

        return timeout < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : timeout;
    }
}
