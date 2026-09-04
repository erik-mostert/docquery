using DocQuery.Application.Abstractions;

namespace DocQuery.Tests.Common;

/// <summary>Records uploads in memory. A failure script can make the first N calls throw, for resilience tests.</summary>
public sealed class FakeBlobStore : IBlobStore
{
    private readonly List<BlobUpload> _uploads = [];
    private readonly Queue<Exception> _failures = new();

    public IReadOnlyList<BlobUpload> Uploads => _uploads;

    public int CallCount { get; private set; }

    /// <summary>Makes the next call throw <paramref name="exception"/>. Call repeatedly to script several failures.</summary>
    public void FailNextCallWith(Exception exception) => _failures.Enqueue(exception);

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        CallCount++;
        var startPosition = content.CanSeek ? content.Position : -1;

        if (_failures.TryDequeue(out var failure))
        {
            throw failure;
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        _uploads.Add(new BlobUpload(blobName, contentType, buffer.Length, startPosition));
    }
}

public sealed record BlobUpload(string BlobName, string ContentType, long Length, long StartPosition);
