using DocQuery.Application.Abstractions;
using DocQuery.Domain.Documents;

namespace DocQuery.Infrastructure.Persistence.Repositories;

internal sealed class DocumentRepository(DocQueryDbContext context) : IDocumentRepository
{
    public async Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        await context.Documents.AddAsync(document, cancellationToken);
    }
}
