# 0018. Each subscription receives only its own message type through a correlation filter on Subject

Date: 2026-09-06

## Status

Accepted

## Context

ADR 0014 put every document event on one topic, `document-events`, with one subscription per worker and left
filtering open. Without a filter every subscription receives every message: the chunking worker sees
`DocumentChunked`, the embedding worker sees `DocumentUploaded`, and each completes what it cannot handle. That
is correct but wasteful, noisy in the logs, and it hides mistakes: a worker missing its handler registration
would silently complete the messages it should process. As more events arrive (`DocumentEmbedded`, future
deletions) the waste grows with the number of workers.

The publisher already stamps `Subject` with the contract type's full name
(`DocQuery.Contracts.Documents.DocumentUploaded`) and consumers route on it, so the routing key exists; only the
broker was not using it.

## Decision

Every subscription carries one correlation filter on `Subject`, matching exactly the full name of the contract
it consumes. The name is computed in one place, `MessageSubject` in `DocQuery.Contracts`, used by the outbox
(publisher), the dispatcher (consumer) and the AppHost (emulator configuration). The Bicep module takes the same
strings as literals, and `MessageSubjectTests` pins them so renaming a contract fails a test before it breaks a
filter.

In Azure, a subscription is created with a catch-all rule named `$Default`; the Bicep defines its rule under
that name, replacing the catch-all. The emulator creates no catch-all when explicit rules are given, so the
AppHost adds a single rule named `subject`.

## Consequences

- Each worker receives only the message type it handles. Unknown-subject handling in the listener stays as a
  safety net and now indicates a configuration error rather than routine traffic.
- A new consumer is added by declaring its subscription and subject in two places (AppHost and Bicep); the
  publisher is untouched, as ADR 0014 intended.
- The emulator reads its configuration only when its container is created. Because the AppHost keeps the
  container across sessions, changing subscriptions or filters requires removing the `servicebus` containers once
  (see README, "Run locally").
- A correlation filter is an exact match, so one subscription cannot receive two message types. A worker that
  needs several would use a SQL filter (`Subject IN (...)`) or several subscriptions; none does today.

## Alternatives considered

- **No filter, consumers ignore what they do not handle.** The state before this ADR. Works, but every message
  is delivered and settled once per worker and misconfiguration is invisible.
- **SQL filter on Subject.** Same result, more expensive to evaluate, and its only advantage (matching several
  subjects) is not needed. Easy to switch to per subscription if it becomes needed.
- **One topic per message type.** Removes the need for filters but multiplies entities, loses the single stream
  of document events and makes the relay publisher aware of topology.
