using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;

namespace DocQuery.Tests.Common;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly List<Document> _documents = [];

    public IReadOnlyCollection<Document> Documents => _documents;

    public Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        _documents.Add(document);
        return Task.CompletedTask;
    }

    public Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken) =>
        Task.FromResult(_documents.FirstOrDefault(document => document.Id == id));
}
