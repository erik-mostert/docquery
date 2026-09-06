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
| [0012](0012-ef-core-postgresql-persistence-with-startup-migrations.md) | EF Core on PostgreSQL, migrations applied at startup, outbox table | Accepted |
| [0013](0013-testcontainers-for-infrastructure-tests.md) | Testcontainers for infrastructure tests, skipped without Docker | Accepted |
| [0014](0014-outbox-relay-worker-and-topic-subscriptions.md) | Outbox relay as its own worker, publishing to a topic with subscriptions | Accepted |
| [0015](0015-chunking-worker-consumer-pattern-and-structure-aware-chunking.md) | Chunking worker: consumer pattern and structure-aware chunking | Accepted |
| [0016](0016-azure-resources-defined-in-bicep.md) | Azure resources are defined in Bicep, one module per service, with secrets in Key Vault | Accepted |
| [0017](0017-embeddings-with-azure-openai-and-pgvector.md) | Embeddings with Azure OpenAI through Microsoft.Extensions.AI, stored in pgvector | Accepted |
| [0018](0018-subscriptions-filter-on-message-subject.md) | Each subscription receives only its own message type through a correlation filter on Subject | Accepted |
