using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace DocQuery.Infrastructure.Resilience;

public static class ResilienceServiceCollectionExtensions
{
    private static readonly PredicateBuilder<object> TransientFailures =
        new PredicateBuilder().Handle<Exception>(exception => exception is not OperationCanceledException);

    /// <summary>Registers the <see cref="ResiliencePipelineNames.BlobStorage"/> pipeline from <c>Resilience:BlobStorage</c>.</summary>
    public static IServiceCollection AddBlobStorageResilience(this IServiceCollection services, IConfiguration configuration) =>
        services.AddConfiguredPipeline<BlobStorageResilienceOptions>(configuration, ResiliencePipelineNames.BlobStorage, BlobStorageResilienceOptions.SectionName);

    /// <summary>Registers the <see cref="ResiliencePipelineNames.ServiceBus"/> pipeline from <c>Resilience:ServiceBus</c>.</summary>
    public static IServiceCollection AddServiceBusResilience(this IServiceCollection services, IConfiguration configuration) =>
        services.AddConfiguredPipeline<ServiceBusResilienceOptions>(configuration, ResiliencePipelineNames.ServiceBus, ServiceBusResilienceOptions.SectionName);

    /// <summary>
    /// Binds <typeparamref name="TOptions"/> from <paramref name="sectionName"/> (validated at startup) and registers a
    /// pipeline of retry (exponential backoff with jitter) wrapping circuit breaker wrapping per-attempt timeout.
    /// Cancellation is never retried.
    /// </summary>
    private static IServiceCollection AddConfiguredPipeline<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string pipelineName,
        string sectionName)
        where TOptions : ResiliencePipelineOptions
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddResiliencePipeline(pipelineName, (builder, context) =>
            Configure(builder, context.ServiceProvider.GetRequiredService<IOptions<TOptions>>().Value));

        return services;
    }

    private static void Configure(ResiliencePipelineBuilder builder, ResiliencePipelineOptions options)
    {
        // Polly rejects MaxRetryAttempts = 0; treat it as "retry disabled".
        if (options.MaxRetryAttempts > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = TransientFailures,
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            });
        }

        builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = TransientFailures,
                FailureRatio = options.FailureRatio,
                SamplingDuration = options.SamplingDuration,
                MinimumThroughput = options.MinimumThroughput,
                BreakDuration = options.BreakDuration,
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.AttemptTimeout,
            });
    }
}
