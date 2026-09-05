# 0013. Testcontainers for infrastructure tests, skipped without Docker

Date: 2026-09-05

## Status

Accepted

## Context

Adapters for Blob Storage and PostgreSQL are only meaningful against the real protocol: an in-memory EF
provider would not catch `jsonb` translation errors, value-converter mistakes or Azurite content-type
behaviour. But the unit-level suites (Domain, Application, API with fakes) must stay fast and runnable
anywhere, including machines where Docker is stopped.

## Decision

- Infrastructure tests run against real emulators started by Testcontainers: `pgvector/pgvector:pg17` for
  persistence and `mcr.microsoft.com/azure-storage/azurite` for blob storage. One container per test
  assembly, shared through an xUnit collection fixture; the database is migrated once by the fixture.
- Such tests use `[DockerFact]` from `DocQuery.Tests.Common`, which sets the xUnit skip reason when no Docker
  engine pipe or socket is present. They are reported as skipped, never as failed, on a machine without Docker.
- The API test host uses the shared fakes and placeholder connection strings; it never talks to a container.
  Startup jobs (migrations, container creation) are switched off through their option flags and the
  dependency health checks are removed so `/health` reflects only the process.
- Migration drift is a test: the model must have no pending changes relative to the last migration.

## Consequences

- First run pulls two images; subsequent runs take a few seconds per assembly.
- A stopped Docker engine silently reduces coverage. CI must have Docker so the skip never masks a
  regression there.
- `WebApplicationFactory` with minimal hosting applies `ConfigureAppConfiguration` only at `Build()`, after
  registration code has run. Settings that registration reads (connection strings, startup flags) are passed
  with `UseSetting`, which is documented in the factory.

## Alternatives considered

- **EF Core InMemory / SQLite providers.** Rejected: different SQL dialect and type system; they pass tests
  that fail on PostgreSQL.
- **A shared long-running local database.** Rejected: state leaks between runs and developers.
- **Fail instead of skip when Docker is absent.** Rejected for the inner loop; CI enforces presence.
