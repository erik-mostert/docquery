namespace DocQuery.Application.Exceptions;

/// <summary>The requested blob does not exist. Not transient: the resilience pipeline does not retry it.</summary>
public sealed class BlobNotFoundException : Exception
{
    public BlobNotFoundException(string blobName)
        : base(FormattableString.Invariant($"Blob '{blobName}' was not found."))
    {
        BlobName = blobName;
    }

    public BlobNotFoundException(string blobName, Exception innerException)
        : base(FormattableString.Invariant($"Blob '{blobName}' was not found."), innerException)
    {
        BlobName = blobName;
    }

    public string BlobName { get; }
}
