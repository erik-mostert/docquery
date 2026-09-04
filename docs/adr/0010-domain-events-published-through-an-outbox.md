# 0010. Domain events raised by aggregates and published through an outbox

Date: 2026-09-04

## Status

Accepted

## Context

After an upload the write side must tell the chunking worker about it (ADR 0004). If the handler publishes to
Service Bus directly after `SaveChanges`, a crash between the two loses the message; publishing before the
commit can announce a document that was never saved. The aggregate is also the natural place to know that
"a document was uploaded" happened.

## Decision

- Aggregates derive from `AggregateRoot` and raise `IDomainEvent` instances from the operation that causes
  them (`Document.Upload` raises `DocumentUploadedEvent`). Handlers never publish anything.
- In the persistence step, the unit of work collects domain events from tracked aggregates when saving,
  converts them to integration messages from `DocQuery.Contracts`, and writes them to an outbox table in the
  same transaction as the state change.
- A relay (hosted service in the Command API, or a worker) reads the outbox and publishes through `IEventBus`,
  marking rows as sent. Delivery is at-least-once; consumers are idempotent.

## Consequences

- The Application layer stays free of messaging concerns; the handler test only checks that the aggregate was
  saved.
- Domain events and integration messages are separate types on purpose: the domain event is internal and can
  change freely, the contract is versioned and shared.
- The outbox adds a table, a relay and a "sent" bookkeeping column, and introduces a short delay between
  commit and publish.

## Alternatives considered

- **Publish directly from the handler after save.** Rejected: dual-write problem.
- **Transactional Service Bus + database.** Rejected: no distributed transaction across PostgreSQL and Service
  Bus.
- **Change data capture from PostgreSQL.** Rejected: operationally heavier than an outbox for a demo.
