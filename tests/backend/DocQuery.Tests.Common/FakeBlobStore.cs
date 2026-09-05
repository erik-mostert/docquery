using DocQuery.Application.Abstractions;
using DocQuery.Application.Exceptions;

namespace DocQuery.Tests.Common;

/// <summary>
/// Records uploads in memory and serves them back for download. A failure script can make upload calls throw after
/// consuming the stream, which simulates a transfer that failed part-way and lets resilience tests prove the stream
/// is rewound.
/// </summary>
public sealed class FakeBlobStore : IBlobStore
{
    private readonly List<BlobUpload> _uploads = [];
    private readonly Dictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);
    private readonly Queue<Exception> _failures = new();

    public IReadOnlyList<BlobUpload> Uploads => _uploads;

    public int CallCount { get; private set; }

    public int DownloadCount { get; private set; }

    /// <summary>Makes the next call throw <paramref name="exception"/>. Call repeatedly to script several failures.</summary>
    public void FailNextCallWith(Exception exception) => _failures.Enqueue(exception);

    /// <summary>Makes <paramref name="bytes"/> downloadable under <paramref name="blobName"/> without an upload.</summary>
    public void Seed(string blobName, byte[] bytes) => _blobs[blobName] = bytes;

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        CallCount++;
        var startPosition = content.CanSeek ? content.Position : -1;

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        if (_failures.TryDequeue(out var failure))
        {
            throw failure;
        }

        _uploads.Add(new BlobUpload(blobName, contentType, buffer.Length, startPosition));
        _blobs[blobName] = buffer.ToArray();
    }

    public Task<Stream> DownloadAsync(string blobName, CancellationToken cancellationToken)
    {
        DownloadCount++;

        if (_failures.TryDequeue(out var failure))
        {
            throw failure;
        }

        if (!_blobs.TryGetValue(blobName, out var bytes))
        {
            throw new BlobNotFoundException(blobName);
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }
}

public sealed record BlobUpload(string BlobName, string ContentType, long Length, long StartPosition);
