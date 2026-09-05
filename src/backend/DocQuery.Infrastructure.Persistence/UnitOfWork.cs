using DocQuery.Application.Abstractions;
using DocQuery.Domain.Common;
using DocQuery.Infrastructure.Persistence.Outbox;

namespace DocQuery.Infrastructure.Persistence;

/// <summary>
/// Commits tracked changes together with outbox rows for every domain event raised by tracked aggregates, so the
/// state change and its integration messages are atomic (ADR 0010). Events are cleared only after a successful save.
/// </summary>
internal sealed class UnitOfWork(
    DocQueryDbContext context,
    IEnumerable<IIntegrationEventMapper> mappers,
    TimeProvider timeProvider) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        var occurredAt = timeProvider.GetUtcNow();

        foreach (var domainEvent in aggregates.SelectMany(aggregate => aggregate.DomainEvents))
        {
            context.OutboxMessages.Add(OutboxMessage.From(Map(domainEvent), occurredAt));
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }
    }

    private object Map(IDomainEvent domainEvent)
    {
        foreach (var mapper in mappers)
        {
            if (mapper.TryMap(domainEvent, out var message))
            {
                return message;
            }
        }

        throw new InvalidOperationException(
            $"No {nameof(IIntegrationEventMapper)} is registered for domain event '{domainEvent.GetType().Name}'.");
    }
}
