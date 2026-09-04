using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;

namespace DocQuery.Application.Documents.Upload;

/// <summary>
/// Stores the PDF, records the <see cref="Document"/> and commits. Blob first: a failure between the two leaves an
/// orphan blob (harmless), never a database row without content. See ADR 0003.
/// </summary>
internal sealed class UploadDocumentHandler(
    IBlobStore blobStore,
    IDocumentRepository documents,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UploadDocumentCommand, DocumentId>
{
    public async Task<DocumentId> HandleAsync(UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var document = Document.Upload(command.FileName, command.ContentType, command.SizeInBytes, timeProvider.GetUtcNow());

        await blobStore.UploadAsync(document.BlobName, command.Content, document.ContentType, cancellationToken);
        await documents.AddAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return document.Id;
    }
}
