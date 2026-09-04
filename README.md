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
    DocQuery.Infrastructure/       adapters and resilience decorators
    DocQuery.Command.Api/          write side: document upload
  clients/
    docquery.web/                  React + Vite UI
tests/
  backend/
    DocQuery.Tests.Common/         shared fakes
    DocQuery.Domain.Tests/
    DocQuery.Application.Tests/
    DocQuery.Infrastructure.Tests/
    DocQuery.Command.Api.Tests/
```

Further projects (contracts, persistence, query API, workers, deployment manifests) are added as the solution
grows.

## Command API

`POST /documents` accepts a multipart form with one `file` part containing a PDF and returns `202 Accepted`
with `{ "documentId": "<guid>" }`. Errors are RFC 9457 problem details: `400` (missing, empty or non-PDF
file), `413` (over `Upload:MaxFileSizeBytes`), `429` (rate limited), `503` with `Retry-After` (storage circuit
open). `GET /health` and `GET /alive` serve readiness and liveness probes.

Cross-cutting behaviour, all configured in `appsettings.json`:

- **Resilience.** Calls to blob storage go through a Polly pipeline (retry with exponential backoff and jitter,
  circuit breaker, per-attempt timeout) configured under `Resilience:BlobStorage`. See ADR 0007.
- **Rate limiting.** Uploads are limited per client IP with a sliding window configured under
  `RateLimiting:Uploads`. Behind an ingress, enable forwarded headers so the real client address is used.
  See ADR 0008.
- **CORS.** Browser origins allowed to call the API are listed under `Cors:AllowedOrigins`; the default is the
  Vite dev server.

**Current limitation:** the storage, repository and unit-of-work implementations are not written yet, so
`POST /documents` fails at runtime until the infrastructure step lands. The endpoint and its behaviour are
covered by tests using in-memory fakes.

## Build and test

```bash
dotnet build
dotnet test
```

## Run locally

Aspire starts the API and the Vite dev server together and opens a dashboard with logs, traces and metrics:

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
