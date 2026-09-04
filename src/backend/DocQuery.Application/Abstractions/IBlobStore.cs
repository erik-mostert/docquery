namespace DocQuery.Application.Abstractions;

/// <summary>Stores document content. Implemented by Azure Blob Storage in Infrastructure.</summary>
public interface IBlobStore
{
    /// <summary>Uploads <paramref name="content"/> under <paramref name="blobName"/>, overwriting any existing blob.</summary>
    Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken);
}
