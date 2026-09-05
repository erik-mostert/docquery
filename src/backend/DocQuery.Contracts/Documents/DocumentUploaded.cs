namespace DocQuery.Contracts.Documents;

/// <summary>Published after a PDF is stored; consumed by the chunking worker.</summary>
public sealed record DocumentUploaded(Guid DocumentId, string BlobName, DateTimeOffset UploadedAt);
