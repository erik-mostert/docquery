using DocQuery.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocQuery.Infrastructure.Persistence.Outbox;

/// <summary>
/// Publishes one batch of pending outbox rows. Rows are locked with <c>FOR UPDATE SKIP LOCKED</c> so several
/// relay instances can run at once without publishing the same message; a failed publish records the error and
/// leaves the row pending for the next run. Delivery is therefore at-least-once (ADR 0014).
/// </summary>
internal sealed class OutboxProcessor(
    DocQueryDbContext context,
    IEventBus eventBus,
    TimeProvider timeProvider,
    IOptions<OutboxRelayOptions> options,
    ILogger<OutboxProcessor> logger)
{
    /// <returns>The number of messages published in this batch.</returns>
    public Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        // The Aspire Npgsql integration enables a retrying execution strategy, which requires user transactions
        // to run inside ExecuteAsync. A retried batch can republish; consumers are idempotent by contract.
        var strategy = context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(ProcessBatchCoreAsync, cancellationToken);
    }

    private async Task<int> ProcessBatchCoreAsync(CancellationToken cancellationToken)
    {
        var batchSize = options.Value.BatchSize;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var pending = await context.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM outbox_messages
                WHERE "ProcessedAt" IS NULL
                ORDER BY "OccurredAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        var published = 0;
        foreach (var message in pending)
        {
            try
            {
                await eventBus.PublishAsync(message.ToOutbound(), cancellationToken);
                message.MarkProcessed(timeProvider.GetUtcNow());
                published++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.MarkFailed(exception.Message);
                OutboxLog.PublishFailed(logger, exception, message.Id, message.Type, message.Attempts);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        context.ChangeTracker.Clear();

        if (published > 0)
        {
            OutboxLog.BatchPublished(logger, published, pending.Count);
        }

        return published;
    }
}
