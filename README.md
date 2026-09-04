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
    DocQuery.Command.Api/          write side: document upload
  clients/
    docquery.web/                  React + Vite UI
tests/
  backend/
    DocQuery.Tests.Common/         shared fakes
    DocQuery.Domain.Tests/
    DocQuery.Application.Tests/
    DocQuery.Command.Api.Tests/
```

Further projects (contracts, persistence, infrastructure, query API, workers, deployment manifests) are added
as the solution grows.

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
