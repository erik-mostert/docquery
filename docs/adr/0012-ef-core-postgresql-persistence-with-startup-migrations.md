# 0012. EF Core on PostgreSQL, migrations applied at startup, outbox table

Date: 2026-09-05

## Status

Accepted

## Context

The write side must persist the `Document` aggregate and, per ADR 0010, write integration messages to an outbox
in the same transaction. The target database is PostgreSQL (Azure Database for PostgreSQL Flexible Server in
production, exam-relevant) with the pgvector extension for the embeddings that come later. The schema needs a
repeatable way to evolve, and local runs should need no manual database setup.

## Decision

- `DocQuery.Infrastructure.Persistence` owns `DocQueryDbContext` (EF Core 10, Npgsql provider), entity
  configurations, repositories, the unit of work and the outbox. The Aspire Npgsql integration supplies the
  connection string, health check, tracing and the retrying execution strategy, so database transient
  faults are retried by EF rather than by Polly (ADR 0007).
- The `Document` aggregate is mapped directly (no separate persistence model): EF binds the private
  constructor by parameter name, `DocumentId` is a value-converted `uuid`, `DomainEvents` is ignored.
- `outbox_messages` stores the contract type name, a `jsonb` payload, the occurrence time, and processing
  state. `UnitOfWork.SaveChangesAsync` maps every pending domain event through an `IIntegrationEventMapper`,
  adds the rows, saves, and only then clears the events. An event without a mapper fails the save.
- Schema changes are EF Core migrations checked into `Migrations/`. A hosted `MigrationRunner` applies pending
  migrations when the host starts, controlled by `Persistence:ApplyMigrationsOnStartup` (default on). Analyzers
  are disabled for the generated migration files.
- The pgvector image (`pgvector/pgvector:pg17`) is used locally from the start so adding the extension later
  is a migration, not an infrastructure change.

## Consequences

- One command starts a working database; tests and the demo never hand-run migrations.
- Applying migrations at startup is unsafe with several replicas starting at once and grants the runtime
  identity DDL rights. The Kubernetes deployment should run migrations from a job (or `dotnet ef` in the
  pipeline) and set `ApplyMigrationsOnStartup=false`; this is recorded as a deployment-step task.
- The outbox needs a relay to publish rows to Service Bus; that arrives with the messaging step and will mark
  rows processed. Until then rows accumulate, which is harmless.
- Mapping the aggregate directly couples the table shape to the domain type. Acceptable for one aggregate;
  a separate persistence model can be introduced per aggregate if the shapes diverge.

## Alternatives considered

- **Dapper or raw SQL.** Rejected: EF migrations and change tracking are what make the outbox cheap.
- **`EnsureCreated` instead of migrations.** Rejected: cannot evolve the schema.
- **Separate migration service project (Aspire pattern).** Deferred; the hosted runner is enough for a demo and
  the option flag leaves room for it.
