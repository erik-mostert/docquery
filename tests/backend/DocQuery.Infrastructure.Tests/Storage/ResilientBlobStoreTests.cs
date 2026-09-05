using DocQuery.Application.Abstractions;
using DocQuery.Infrastructure;
using DocQuery.Infrastructure.Resilience;
using DocQuery.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;

namespace DocQuery.Infrastructure.Tests.Storage;

public sealed class ResilientBlobStoreTests
{
    private static readonly byte[] Content = "%PDF-1.4 resilient"u8.ToArray();

    [Fact]
    public async Task Retries_transient_failure_and_rewinds_stream_before_each_attempt()
    {
        var (store, inner) = Build(maxRetryAttempts: 3);
        inner.FailNextCallWith(new IOException("blip 1"));
        inner.FailNextCallWith(new IOException("blip 2"));
        using var stream = new MemoryStream(Content);

        await store.UploadAsync("doc.pdf", stream, "application/pdf", CancellationToken.None);

        Assert.Equal(3, inner.CallCount);
        var upload = Assert.Single(inner.Uploads);
        Assert.Equal(0, upload.StartPosition);
        Assert.Equal(Content.Length, upload.Length);
    }

    [Fact]
    public async Task Gives_up_after_max_retry_attempts_and_surfaces_original_exception()
    {
        var (store, inner) = Build(maxRetryAttempts: 2);
        for (var i = 0; i < 10; i++)
        {
            inner.FailNextCallWith(new IOException("down"));
        }

        using var stream = new MemoryStream(Content);

        var exception = await Assert.ThrowsAsync<IOException>(
            () => store.UploadAsync("doc.pdf", stream, "application/pdf", CancellationToken.None));

        Assert.Equal("down", exception.Message);
        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public async Task Opens_circuit_after_failure_threshold_and_stops_calling_inner_store()
    {
        var (store, inner) = Build(maxRetryAttempts: 0, minimumThroughput: 2, failureRatio: 1.0);
        for (var i = 0; i < 10; i++)
        {
            inner.FailNextCallWith(new IOException("down"));
        }

        for (var i = 0; i < 2; i++)
        {
            using var failing = new MemoryStream(Content);
            await Assert.ThrowsAsync<IOException>(
                () => store.UploadAsync("doc.pdf", failing, "application/pdf", CancellationToken.None));
        }

        using var stream = new MemoryStream(Content);
        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => store.UploadAsync("doc.pdf", stream, "application/pdf", CancellationToken.None));

        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Rejects_non_seekable_stream_before_calling_inner_store()
    {
        var (store, inner) = Build(maxRetryAttempts: 3);
        using var stream = new NonSeekableStream(Content);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.UploadAsync("doc.pdf", stream, "application/pdf", CancellationToken.None));

        Assert.Equal(0, inner.CallCount);
    }

    [Fact]
    public async Task Does_not_retry_cancellation()
    {
        var (store, inner) = Build(maxRetryAttempts: 3);
        inner.FailNextCallWith(new OperationCanceledException());
        using var stream = new MemoryStream(Content);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.UploadAsync("doc.pdf", stream, "application/pdf", CancellationToken.None));

        Assert.Equal(1, inner.CallCount);
    }

    private static (IBlobStore Store, FakeBlobStore Inner) Build(
        int maxRetryAttempts,
        int minimumThroughput = 100,
        double failureRatio = 1.0)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resilience:BlobStorage:MaxRetryAttempts"] = maxRetryAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Resilience:BlobStorage:BaseDelay"] = "00:00:00.001",
                ["Resilience:BlobStorage:AttemptTimeout"] = "00:00:05",
                ["Resilience:BlobStorage:FailureRatio"] = failureRatio.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Resilience:BlobStorage:SamplingDuration"] = "00:00:10",
                ["Resilience:BlobStorage:MinimumThroughput"] = minimumThroughput.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Resilience:BlobStorage:BreakDuration"] = "00:00:30",
            })
            .Build();

        var inner = new FakeBlobStore();
        var services = new ServiceCollection();
        services.AddBlobStorageResilience(configuration);
        services.AddResilientBlobStore();
        services.AddKeyedSingleton<IBlobStore>(ServiceKeys.RawBlobStore, inner);

        var store = services.BuildServiceProvider().GetRequiredService<IBlobStore>();
        return (store, inner);
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
