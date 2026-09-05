using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocQuery.Infrastructure.Persistence.Outbox;

/// <summary>
/// Polls the outbox on a timer. Each tick drains the table batch by batch until a batch comes back short, then
/// waits for the next tick. A failing cycle is logged and retried; it never stops the host.
/// </summary>
internal sealed class OutboxRelayService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxRelayOptions> options,
    ILogger<OutboxRelayService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval);

        try
        {
            do
            {
                await DrainAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

            int published;
            do
            {
                published = await processor.ProcessBatchAsync(cancellationToken);
            }
            while (published == options.Value.BatchSize && !cancellationToken.IsCancellationRequested);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            OutboxLog.RelayCycleFailed(logger, exception);
        }
    }
}
