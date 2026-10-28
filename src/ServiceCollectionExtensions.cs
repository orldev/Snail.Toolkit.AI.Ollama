using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly.Retry;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Configuration;

using ITextToSpeechClient = Microsoft.Extensions.AI.ITextToSpeechClient;

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

        /// <summary>
        /// Registers Ollama's text-to-speech as MEAI's <see cref="ITextToSpeechClient"/>, with the settings AddOllama
        /// configured.
        /// </summary>
        /// <remarks>
        /// A call of its own because MEAI's speech abstractions are experimental (MEAI001): an application opts in to
        /// them, and AddOllama keeps registering only what is stable. Speech is never retried — a retry would replay
        /// audio half played.
        /// </remarks>
        [Experimental("MEAI001")]
        public IServiceCollection AddOllamaSpeech()
        {
            services.AddHttpClient<ITextToSpeechClient, SpeechClient>(ConfigureTransport)
                .ConfigurePrimaryHttpMessageHandler(CreateStreamingSafeHandler);

            return services;
        }

        private IServiceCollection AddClients()
        {
            services.AddOptions<OllamaOptions>().ValidateOnStart();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OllamaOptions>, OllamaOptionsValidator>());

            services.AddHttpClient<IChatClient, ChatClient>(ConfigureTransport)
                .ConfigurePrimaryHttpMessageHandler(CreateStreamingSafeHandler);

            services.AddHttpClient<IGenerateClient, GenerateClient>(ConfigureTransport)
                .ConfigurePrimaryHttpMessageHandler(CreateStreamingSafeHandler);

            services.AddHttpClient<IEmbeddingsClient, EmbeddingsClient>(ConfigureTransport)
                .ConfigurePrimaryHttpMessageHandler(CreateStreamingSafeHandler)
                .AddRetriesForIdempotentCalls();

            services.AddHttpClient<ISystemOneClient, SystemOneClient>(ConfigureTransport)
                .ConfigurePrimaryHttpMessageHandler(CreateStreamingSafeHandler)
                .AddRetriesForIdempotentCalls();

            services.AddHttpClient<IModelsClient, ModelsClient>(ConfigureTransport)
                .ConfigurePrimaryHttpMessageHandler(CreateStreamingSafeHandler)
                .AddRetriesForIdempotentCalls();

            services.AddHttpClient<IAccountClient, AccountClient>(ConfigureTransport)
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
    /// Embeddings, System One scoring, model queries and account reports are the only idempotent calls — chat and
    /// generation stream, and a retry would replay a half-consumed generation.
    /// </summary>
    /// <remarks>
    /// OllamaOptions.Timeout is the budget of the whole call, retries included, so every resilience timeout
    /// is derived from it. Fixed budgets contradicted it: a 2-minute attempt under a 100-second call could
    /// never be retried, and a 5-minute total silently capped any Timeout set above it. An attempt may use
    /// the full budget because a slow local model is still working — only a fast failure is worth another
    /// try. The circuit breaker must sample at least two attempts' worth of time. Every retried failure is
    /// recorded, so a budget that runs out between attempts reports the cause instead of a bare cancellation.
    /// </remarks>
    private static void AddRetriesForIdempotentCalls(this IHttpClientBuilder builder) =>
        builder.AddStandardResilienceHandler().Configure((resilience, provider) =>
        {
            TimeSpan budget = ResilienceBudget(provider.GetRequiredService<IOptions<OllamaOptions>>().Value.Timeout);

            resilience.TotalRequestTimeout.Timeout = budget;
            resilience.AttemptTimeout.Timeout = budget;
            resilience.CircuitBreaker.SamplingDuration = budget * 2;
            resilience.Retry.OnRetry = RecordFailedAttempt;
        });

    /// <summary>
    /// Records why an attempt is retried: its exception, or the status Ollama answered with.
    /// </summary>
    private static ValueTask RecordFailedAttempt(OnRetryArguments<HttpResponseMessage> retry)
    {
        var failure = retry.Outcome.Exception
            ?? new HttpRequestException($"Ollama answered {retry.Outcome.Result?.StatusCode}.", null, retry.Outcome.Result?.StatusCode);

        FailedAttempts.Record(failure);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// The call budget held inside the ranges Polly validates: anything shorter than a second becomes one second.
    /// </summary>
    private static TimeSpan ResilienceBudget(TimeSpan timeout)
    {
        TimeSpan budget = CallBudget.Of(timeout);
        TimeSpan shortest = TimeSpan.FromSeconds(1);

        return budget < shortest ? shortest : budget;
    }
}
