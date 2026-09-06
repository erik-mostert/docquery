using DocQuery.Domain.Documents;

namespace DocQuery.Application.Abstractions;

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken);

    Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken);

    /// <summary>The most recently uploaded documents, newest first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<Document>> ListAsync(int limit, CancellationToken cancellationToken);
}
