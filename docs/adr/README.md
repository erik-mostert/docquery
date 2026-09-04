# Architecture Decision Records

Design decisions for DocQuery, one file per decision, in the order they were made. Accepted records are not
edited; a change of mind gets a new record that supersedes the old one.

Template: [template.md](template.md)

| # | Decision | Status |
|---|---|---|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-clean-architecture-with-cqrs.md) | Clean Architecture with CQRS split into Command and Query APIs | Accepted |
| [0003](0003-api-streams-uploads-to-blob-storage.md) | Command API streams PDF uploads to Blob Storage | Accepted |
| [0004](0004-service-bus-behind-event-bus-abstraction.md) | Azure Service Bus behind an `IEventBus` abstraction | Accepted |
| [0005](0005-no-authentication-in-demo.md) | No authentication or authorization in the demo | Accepted |
| [0006](0006-aspire-for-local-orchestration.md) | Aspire for local orchestration | Accepted |
| [0007](0007-polly-resilience-for-outbound-dependencies.md) | Polly retry and circuit breaker around outbound dependencies | Accepted |
| [0008](0008-rate-limiting-with-aspnet-core-rate-limiter.md) | Inbound rate limiting with the ASP.NET Core rate limiter | Accepted |
| [0009](0009-hand-rolled-command-handlers-and-endpoint-classes.md) | Hand-rolled command handlers and one class per endpoint | Accepted |
| [0010](0010-domain-events-published-through-an-outbox.md) | Domain events raised by aggregates and published through an outbox | Accepted |
| [0011](0011-upload-returns-202-accepted.md) | Upload returns 202 Accepted | Accepted |
