# DocQuery

Demo solution built to exercise concepts from the AI-200, AZ-104 and AZ-305 exams.

Users upload PDF documents through a React UI. Each document is stored in Azure Blob Storage and a
`DocumentUploaded` message is published to Azure Service Bus. A chunking worker splits the document,
publishes `DocumentChunked`, and an embedding worker generates embeddings with Azure OpenAI and stores
them in PostgreSQL (pgvector). A query API answers natural-language questions over the documents using a
RAG pipeline. All services are containerised and deployed to Kubernetes.

## Design decisions

Recorded as Architecture Decision Records in [docs/adr](docs/adr/README.md).

## Demo scope

This is a demonstration project. It deliberately omits concerns that any production system must have:

- **Authentication and authorization.** No endpoint is protected. A production deployment should put
  the APIs behind Microsoft Entra ID (or another identity provider) and enforce per-user document access.
- **Rate limiting, request size limits tuned per environment, secrets management, and network isolation**
  are likewise out of scope.

## Structure

```
DocQuery.slnx
docs/adr/                          architecture decision records
src/
  DocQuery.AppHost/                Aspire orchestration for local runs
  DocQuery.ServiceDefaults/        telemetry, health checks, resilience defaults shared by all hosts
  backend/
    DocQuery.Domain/               entities, value objects, domain events
    DocQuery.Application/          commands, handlers, abstractions
    DocQuery.Contracts/            integration message contracts shared with workers
    DocQuery.Infrastructure/       Azure Blob Storage adapter and resilience decorators
    DocQuery.Infrastructure.Persistence/  EF Core + PostgreSQL, migrations, outbox
    DocQuery.Command.Api/          write side: document upload
  clients/
    docquery.web/                  React + Vite UI
tests/
  backend/
    DocQuery.Tests.Common/         shared fakes
    DocQuery.Domain.Tests/
    DocQuery.Application.Tests/
    DocQuery.Infrastructure.Tests/            includes Azurite-backed tests (Docker)
    DocQuery.Infrastructure.Persistence.Tests/  PostgreSQL-backed tests (Docker)
    DocQuery.Command.Api.Tests/
```

Further projects (query API, workers, deployment manifests) are added as the solution grows.

## Command API

`POST /documents` accepts a multipart form with one `file` part containing a PDF and returns `202 Accepted`
with `{ "documentId": "<guid>" }`. Errors are RFC 9457 problem details: `400` (missing, empty or non-PDF
file), `413` (over `Upload:MaxFileSizeBytes`), `429` (rate limited), `503` with `Retry-After` (storage circuit
open). `GET /health` and `GET /alive` serve readiness and liveness probes.

Cross-cutting behaviour, all configured in `appsettings.json`. Every option section is bound to a typed options
class with range annotations and validated when the host starts, so an out-of-range value stops the process
with a clear message instead of failing the first request:

- **Resilience.** Calls to blob storage go through a Polly pipeline (retry with exponential backoff and jitter,
  circuit breaker, per-attempt timeout) configured under `Resilience:BlobStorage`. See ADR 0007.
- **Rate limiting.** Uploads are limited per client IP with a sliding window configured under
  `RateLimiting:Uploads`. Behind an ingress, enable forwarded headers so the real client address is used.
  See ADR 0008.
- **CORS.** Browser origins allowed to call the API are listed under `Cors:AllowedOrigins`; the default is the
  Vite dev server.

Uploads are stored in the `documents` blob container (Azurite locally) and recorded in PostgreSQL together
with an outbox row carrying the `DocumentUploaded` contract. A relay that publishes outbox rows to Azure
Service Bus is the next step, so rows currently accumulate unprocessed.

## Build and test

```bash
dotnet build
dotnet test
```

Infrastructure tests start PostgreSQL and Azurite containers with Testcontainers. Without a running Docker
engine they are reported as skipped, not failed.

## Run locally

Aspire starts Azurite, PostgreSQL (pgvector image), the API and the Vite dev server together and opens a
dashboard with logs, traces and metrics. Docker Desktop must be running for the emulators. Both emulators use
named Docker volumes and a persistent container lifetime, so uploaded documents and database rows survive
between sessions and migrations run only once. To start clean, stop Aspire and remove the `docquery` containers
and volumes (`docker ps -a`, `docker volume ls`). The generated PostgreSQL password lives in the AppHost user
secrets, so pgAdmin can connect with the credentials shown on the resource in the dashboard.

```bash
dotnet run --project src/DocQuery.AppHost
```

The UI can also be run on its own:

```bash
cd src/clients/docquery.web
npm install
npm run dev
```

### Troubleshooting

**Aspire fails with `x509: certificate signed by unknown authority` or `DCP controller host exited unexpectedly`.**
Aspire's orchestrator (`dcp.exe`) talks to itself over TLS on `127.0.0.1`. Antivirus products that inspect
local TLS traffic (observed with Norton) re-sign that connection and DCP rejects it. Exclude `dcp.exe` and
`dotnet.exe` from the antivirus TLS/HTTPS inspection feature, or disable that feature, then run again.
Diagnostic logs can be captured by setting `DCP_DIAGNOSTICS_LOG_FOLDER` to a directory before running.
