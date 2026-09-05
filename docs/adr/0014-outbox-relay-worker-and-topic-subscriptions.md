# 0014. Outbox relay as its own worker, publishing to a topic with subscriptions

Date: 2026-09-05

## Status

Accepted

## Context

ADR 0010 writes integration messages to an `outbox_messages` table in the same transaction as the state change.
Something must move those rows to Azure Service Bus (ADR 0004). Open questions were where that relay runs, how it
stays correct with several instances, and what Service Bus entities the messages land on.

## Decision

**Relay host.** `DocQuery.Workers.OutboxRelay` is a separate worker (generic host) rather than a hosted service
inside the Command API. It scales independently (normally one replica), keeps the API request-only, and follows
the "one worker project per background job" rule of ADR 0002. The relay code itself lives in
`DocQuery.Infrastructure.Persistence` (it needs the DbContext); the worker is composition only, and its
composition is a public method so a test builds the same host.

**Relay algorithm.** `OutboxRelayService` polls on a timer (`Outbox:PollInterval`). Each cycle drains the table
batch by batch until a batch comes back short. `OutboxProcessor` handles one batch inside a transaction:
`SELECT … WHERE "ProcessedAt" IS NULL ORDER BY "OccurredAt" LIMIT n FOR UPDATE SKIP LOCKED`, publish each row
through `IEventBus`, mark it processed, commit. A failed publish records `Error` and increments `Attempts`; the
row stays pending and is retried next cycle. One failure does not block the rest of the batch.

- `SKIP LOCKED` makes concurrent relay instances safe without a leader election.
- Publish happens before the row is marked, so delivery is at-least-once. Consumers must be idempotent; the
  outbox id is the Service Bus `MessageId`, so duplicate detection on the topic removes most repeats.
- The batch runs under EF Core's retrying execution strategy, which the Aspire Npgsql integration enables and
  which requires user transactions to be wrapped.
- Rows are never deleted; retention/archival is a later concern.
- The relay polls rather than reacting to the domain event, because the event exists only inside the API
  process. The outbox row is the durable form of the event and polling is how a separate process observes it
  without a lost-signal failure mode. PostgreSQL `LISTEN`/`NOTIFY` can be added later to cut latency, with the
  timer kept as the fallback sweep.

**Message envelope.** Body = the JSON payload from the outbox row. `MessageId` = outbox id. `Subject` = contract
type full name (consumers route on it). `ContentType` = `application/json`. `ApplicationProperties["OccurredAt"]`
= ISO-8601 timestamp.

**Service Bus entities.** One topic, `document-events`, carries every document message. Each worker owns a
subscription (`chunking` now; `embedding` later), optionally with a SQL filter on `Subject`. Topics and
subscriptions are the AZ-104/AZ-305 pattern for fan-out and let a new consumer be added without touching
publishers.

**Resilience.** `ServiceBusEventBus` is wrapped by `ResilientEventBus` with its own Polly pipeline
(`Resilience:ServiceBus`), the SDK's retries disabled, mirroring the blob adapter. Resilience options share a base
class so every dependency gets the same validated settings shape.

## Consequences

- One more project, Dockerfile and Kubernetes Deployment. Acceptable; it is the demonstrable unit of scale.
- The relay depends on the API having applied migrations; locally the AppHost starts it after the API. In
  Kubernetes a migration job should run first (ADR 0012).
- Messages sit in the outbox for up to one poll interval. Fine for a document pipeline.
- Locally the Service Bus emulator pulls an SQL Edge sidecar; first start is slow.

## Alternatives considered

- **Relay inside the Command API.** Rejected: scales with request replicas for no benefit, mixes concerns.
- **Queue per message type.** Rejected: a new consumer means a new queue and a publisher change.
- **Delete rows after publish.** Rejected for now: the table doubles as an audit trail while the pipeline is
  being built.
- **Leader election instead of `SKIP LOCKED`.** Rejected: more machinery for the same guarantee.
