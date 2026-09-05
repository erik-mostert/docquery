using DocQuery.Contracts.Documents;
using DocQuery.Infrastructure.Persistence.Outbox;
using DocQuery.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocQuery.Infrastructure.Persistence.Tests.Outbox;

/// <summary>Each test gets its own migrated database so pending rows from other tests cannot leak in.</summary>
[Collection(PostgresCollectionDefinition.Name)]
public sealed class OutboxProcessorTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = PostgresFixture.Now;

    [DockerFact]
    public async Task Publishes_pending_messages_oldest_first_and_marks_them_processed()
    {
        var connectionString = await postgres.CreateFreshDatabaseAsync(migrate: true);
        var ids = await SeedPendingAsync(connectionString, count: 3);
        var bus = new FakeEventBus();

        var published = await RunAsync(connectionString, bus, batchSize: 50);

        Assert.Equal(3, published);
        Assert.Equal(ids, bus.Published.Select(m => m.MessageId).ToList());
        Assert.All(bus.Published, m => Assert.Equal(typeof(DocumentUploaded).FullName, m.Type));
        Assert.All(bus.Published, m => Assert.Contains("blobName", m.Payload, StringComparison.Ordinal));

        await using var context = PostgresFixture.CreateContext(connectionString);
        var rows = await context.OutboxMessages.ToListAsync();
        Assert.All(rows, row => Assert.Equal(Now, row.ProcessedAt));
    }

    [DockerFact]
    public async Task Failed_publish_records_error_and_attempt_and_leaves_message_pending()
    {
        var connectionString = await postgres.CreateFreshDatabaseAsync(migrate: true);
        var ids = await SeedPendingAsync(connectionString, count: 1);
        var bus = new FakeEventBus();
        bus.FailNextCallWith(new IOException("bus down"));

        var firstRun = await RunAsync(connectionString, bus, batchSize: 50);

        Assert.Equal(0, firstRun);
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            var row = await context.OutboxMessages.SingleAsync(m => m.Id == ids[0]);
            Assert.Null(row.ProcessedAt);
            Assert.Equal("bus down", row.Error);
            Assert.Equal(1, row.Attempts);
        }

        var secondRun = await RunAsync(connectionString, bus, batchSize: 50);

        Assert.Equal(1, secondRun);
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            var row = await context.OutboxMessages.SingleAsync(m => m.Id == ids[0]);
            Assert.Equal(Now, row.ProcessedAt);
            Assert.Null(row.Error);
            Assert.Equal(1, row.Attempts);
        }
    }

    [DockerFact]
    public async Task One_failure_does_not_block_the_rest_of_the_batch()
    {
        var connectionString = await postgres.CreateFreshDatabaseAsync(migrate: true);
        await SeedPendingAsync(connectionString, count: 3);
        var bus = new FakeEventBus();
        bus.FailNextCallWith(new IOException("first one fails"));

        var published = await RunAsync(connectionString, bus, batchSize: 50);

        Assert.Equal(2, published);
        Assert.Equal(2, bus.Published.Count);
    }

    [DockerFact]
    public async Task Takes_at_most_batch_size_messages_per_run()
    {
        var connectionString = await postgres.CreateFreshDatabaseAsync(migrate: true);
        await SeedPendingAsync(connectionString, count: 5);
        var bus = new FakeEventBus();

        var published = await RunAsync(connectionString, bus, batchSize: 2);

        Assert.Equal(2, published);
        await using var context = PostgresFixture.CreateContext(connectionString);
        Assert.Equal(3, await context.OutboxMessages.CountAsync(m => m.ProcessedAt == null));
    }

    [DockerFact]
    public async Task Ignores_already_processed_messages()
    {
        var connectionString = await postgres.CreateFreshDatabaseAsync(migrate: true);
        await SeedPendingAsync(connectionString, count: 1);
        var bus = new FakeEventBus();
        await RunAsync(connectionString, bus, batchSize: 50);

        var secondRun = await RunAsync(connectionString, bus, batchSize: 50);

        Assert.Equal(0, secondRun);
        Assert.Equal(1, bus.CallCount);
    }

    [DockerFact]
    public async Task Concurrent_processors_never_publish_the_same_message_twice()
    {
        var connectionString = await postgres.CreateFreshDatabaseAsync(migrate: true);
        await SeedPendingAsync(connectionString, count: 40);
        var bus = new FakeEventBus();

        var runs = Enumerable.Range(0, 4).Select(_ => DrainAsync(connectionString, bus, batchSize: 5));
        var totals = await Task.WhenAll(runs);

        Assert.Equal(40, totals.Sum());
        Assert.Equal(40, bus.Published.Select(m => m.MessageId).Distinct().Count());
    }

    private static async Task<int> DrainAsync(string connectionString, FakeEventBus bus, int batchSize)
    {
        var total = 0;
        int published;
        do
        {
            published = await RunAsync(connectionString, bus, batchSize);
            total += published;
        }
        while (published > 0);

        return total;
    }

    private static async Task<int> RunAsync(string connectionString, FakeEventBus bus, int batchSize)
    {
        await using var context = PostgresFixture.CreateContext(connectionString);
        var processor = new OutboxProcessor(
            context,
            bus,
            new FixedTimeProvider(Now),
            Options.Create(new OutboxRelayOptions { BatchSize = batchSize }),
            NullLogger<OutboxProcessor>.Instance);

        return await processor.ProcessBatchAsync(CancellationToken.None);
    }

    private static async Task<List<Guid>> SeedPendingAsync(string connectionString, int count)
    {
        await using var context = PostgresFixture.CreateContext(connectionString);
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var occurredAt = Now.AddSeconds(i - count);
            var message = OutboxMessage.From(new DocumentUploaded(Guid.NewGuid(), $"{i}.pdf", occurredAt), occurredAt);
            context.OutboxMessages.Add(message);
            ids.Add(message.Id);
        }

        await context.SaveChangesAsync();
        return ids;
    }
}
