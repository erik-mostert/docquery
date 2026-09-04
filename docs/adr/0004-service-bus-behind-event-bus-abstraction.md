# 0004. Azure Service Bus behind an `IEventBus` abstraction

Date: 2026-09-04

## Status

Accepted

## Context

Uploads, chunking and embedding are decoupled by messages. Azure Service Bus is the exam-relevant choice
(AZ-104, AZ-305) and has an emulator that Aspire can run locally. The project owner also wants the transport to be
swappable so the code is not welded to one broker.

## Decision

The Application layer defines `IEventBus` (publish) and the subscriber contracts it needs. Infrastructure provides
the Azure Service Bus implementation. Tests use an in-memory implementation. Swapping brokers is a single
dependency-injection registration plus a new Infrastructure adapter.

Message contracts live in `DocQuery.Contracts` so publishers and subscribers share types without depending on
each other's Application layer.

Publishing from the write side goes through an outbox: domain events raised by aggregates are converted to
integration messages in the same database transaction as the state change, and a relay publishes them to the bus.

## Consequences

- No direct `Azure.Messaging.ServiceBus` usage outside Infrastructure.
- The outbox adds a table and a relay process but removes the dual-write problem between database and bus.
- Consumers must be idempotent; the outbox guarantees at-least-once delivery.

## Alternatives considered

- **MassTransit or Wolverine.** Not rejected outright; they provide outbox, retries and transport abstraction out
  of the box. Deferred because hand-rolling the abstraction is a better teaching vehicle for a small demo. A later
  ADR may adopt one.
- **RabbitMQ or Kafka.** Rejected as the default: less exam-relevant. Still possible behind `IEventBus`.
- **Direct publish without outbox.** Rejected: a crash between the database commit and the publish loses the
  message.
