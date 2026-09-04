namespace DocQuery.Command.Api.Endpoints.Documents;

/// <summary>Returned with 202 Accepted: the document is stored and queued for processing.</summary>
public sealed record UploadDocumentResponse(Guid DocumentId);
