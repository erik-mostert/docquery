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
    /// <summary>
    /// Registers the <see cref="ResiliencePipelineNames.BlobStorage"/> pipeline: retry (exponential backoff with
    /// jitter) wrapping circuit breaker wrapping per-attempt timeout. Cancellation is never retried.
    /// </summary>
    public static IServiceCollection AddBlobStorageResilience(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<BlobStorageResilienceOptions>(configuration.GetSection(BlobStorageResilienceOptions.SectionName));

        services.AddResiliencePipeline(ResiliencePipelineNames.BlobStorage, (builder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<BlobStorageResilienceOptions>>().Value;

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
        });

        return services;
    }

    private static readonly PredicateBuilder<object> TransientFailures =
        new PredicateBuilder().Handle<Exception>(exception => exception is not OperationCanceledException);
}
