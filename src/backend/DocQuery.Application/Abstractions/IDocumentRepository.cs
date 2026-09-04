using DocQuery.Domain.Documents;

namespace DocQuery.Application.Abstractions;

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken);
}
