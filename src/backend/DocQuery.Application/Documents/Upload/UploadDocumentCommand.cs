namespace DocQuery.Application.Documents.Upload;

/// <summary>Upload a PDF. <paramref name="Content"/> is owned by the caller and must remain open while the command runs.</summary>
public sealed record UploadDocumentCommand(string FileName, string ContentType, long SizeInBytes, Stream Content);
