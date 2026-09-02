using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private static void ConfigureTransport(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<OllamaOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl);
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
    /// Embeddings are the only idempotent calls — chat and generation stream, and a retry
    /// would replay a half-consumed generation. Budgets are sized for slow local models.
    /// </summary>
    private static void AddRetriesForIdempotentCalls(this IHttpClientBuilder builder) =>
        builder.AddStandardResilienceHandler(resilience =>
        {
            resilience.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
            resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(4);
            resilience.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
        });
}
