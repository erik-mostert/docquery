namespace DocQuery.Application.Abstractions;

/// <summary>Commits pending changes (and, in Infrastructure, the outbox messages derived from domain events).</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
