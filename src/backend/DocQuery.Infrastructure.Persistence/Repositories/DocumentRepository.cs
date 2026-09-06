using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocQuery.Infrastructure.Persistence.Repositories;

internal sealed class DocumentRepository(DocQueryDbContext context) : IDocumentRepository
{
    public async Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        await context.Documents.AddAsync(document, cancellationToken);
    }

    public Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken) =>
        context.Documents.SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Document>> ListAsync(int limit, CancellationToken cancellationToken) =>
        await context.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.UploadedAt)
            .ThenByDescending(document => document.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
