using System.Diagnostics.CodeAnalysis;
using DocQuery.Domain.Common;

namespace DocQuery.Infrastructure.Persistence.Outbox;

/// <summary>
/// Translates a domain event into the integration message contract that goes on the bus. One implementation per
/// event type; the unit of work fails the save when an event has no mapper, so a forgotten contract is caught early.
/// </summary>
public interface IIntegrationEventMapper
{
    bool TryMap(IDomainEvent domainEvent, [NotNullWhen(true)] out object? message);
}
