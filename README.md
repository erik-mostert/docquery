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
infra/bicep/                       Azure resources as Bicep modules (see infra/README.md)
tools/                             single-file diagnostic programs (see tools/README.md)
src/
  DocQuery.AppHost/                Aspire orchestration for local runs
  DocQuery.ServiceDefaults/        telemetry, health checks, resilience defaults shared by all hosts
  backend/
    DocQuery.Domain/               entities, value objects, domain events
    DocQuery.Application/          commands, queries, handlers, abstractions
    DocQuery.Contracts/            integration message contracts shared with workers
    DocQuery.Infrastructure/       Blob Storage, Service Bus (publish and consume), PDF extraction, tokenizer, Azure OpenAI
    DocQuery.Infrastructure.Persistence/  EF Core + PostgreSQL, migrations, outbox table and relay
    DocQuery.Api.Common/           endpoint discovery, health, problem details, CORS, rate limiting shared by the APIs
    DocQuery.Command.Api/          write side: document upload
    DocQuery.Query.Api/            read side: questions answered from the documents (RAG)
    DocQuery.Workers.OutboxRelay/  publishes outbox rows to Service Bus
    DocQuery.Workers.Chunking/     consumes DocumentUploaded, extracts text, writes chunks
    DocQuery.Workers.Embedding/    consumes DocumentChunked, embeds chunks with Azure OpenAI
  clients/
    docquery.web/                  React + Vite UI
tests/
  backend/
    DocQuery.Tests.Common/         shared fakes
    DocQuery.Domain.Tests/
    DocQuery.Application.Tests/
    DocQuery.Infrastructure.Tests/            includes Azurite and Service Bus emulator tests (Docker)
    DocQuery.Infrastructure.Persistence.Tests/  PostgreSQL-backed tests (Docker)
    DocQuery.Command.Api.Tests/
    DocQuery.Query.Api.Tests/
    DocQuery.Workers.OutboxRelay.Tests/
    DocQuery.Workers.Chunking.Tests/
    DocQuery.Workers.Embedding.Tests/
```

Deployment manifests are added with the Kubernetes step.

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
with an outbox row carrying the `DocumentUploaded` contract.

## Outbox relay worker

`DocQuery.Workers.OutboxRelay` polls `outbox_messages` (every `Outbox:PollInterval`, `Outbox:BatchSize` rows at
a time under `FOR UPDATE SKIP LOCKED`) and publishes each row to the Service Bus topic `Messaging:TopicName`
(`document-events`). Body is the JSON payload, `MessageId` the outbox id, `Subject` the contract type name.
Failed rows record the error and attempt count and are retried on the next poll; delivery is at-least-once.
Publishing goes through its own Polly pipeline under `Resilience:ServiceBus`. See ADR 0014.

## Chunking worker

`DocQuery.Workers.Chunking` (Aspire resource `chunking-worker`) consumes `DocumentUploaded` from the `chunking` subscription. It downloads the PDF,
extracts text per page with PdfPig (layout blocks in reading order), splits it into chunks of at most
`Chunking:MaxTokens` tokens (`o200k_base`, the Azure OpenAI encoding) and stores them in `document_chunks`. The
document becomes `Chunked` and a `DocumentChunked` message goes through the outbox.

Chunks respect structure: whole paragraphs and lists are packed together, a list is split only between items when
it alone exceeds a chunk, sentences are the next boundary, and raw token windows are the last resort. Chunks never
cross pages. Detection of lists is a heuristic over leading characters and layout; see ADR 0015 for the limits.

Consumer conventions (shared by future workers): messages route by `Subject`; unknown subjects are completed and
ignored; permanent failures (missing document or blob, unparseable PDF, no text) mark the document `Failed` and
dead-letter the message with the reason; everything else is abandoned for redelivery, up to five attempts.

## Embedding worker

`DocQuery.Workers.Embedding` (Aspire resource `embedding-worker`) consumes `DocumentChunked` from the `embedding`
subscription, embeds every chunk without a vector with Azure OpenAI `text-embedding-3-small` (1536 dimensions,
`Embedding:BatchSize` chunks per call) through `Microsoft.Extensions.AI`, stores the vectors in the
`document_chunks.Embedding` pgvector column (HNSW cosine index), marks the document `Embedded` and raises
`DocumentEmbedded` through the outbox. Calls to Azure OpenAI go through a Polly pipeline under
`Resilience:AzureOpenAI`. See ADR 0017.

Azure OpenAI has no emulator, so this worker needs a real deployment. Two ways to give it the connection string:

- **Key Vault (recommended, matches the Bicep in `infra/`)**: store only the vault URI as an AppHost user secret;
  the worker reads `ConnectionStrings--openai` from the vault through your `az login` identity. Connection strings
  Aspire injects take precedence over the vault, so storage, messaging and the database stay on the emulators.

  ```bash
  dotnet user-secrets set "ConnectionStrings:keyvault" "https://<vault>.vault.azure.net/" --project src/DocQuery.AppHost
  ```

- **Direct**: put the connection string itself in AppHost user secrets.

  ```bash
  dotnet user-secrets set "ConnectionStrings:openai" "Endpoint=https://<resource>.openai.azure.com/;Key=<key>" --project src/DocQuery.AppHost
  ```

Without either, the rest of the stack still runs and the embedding worker stops at startup with a message naming
the missing key. The opt-in live test `AzureOpenAIEmbeddingSmokeTests` runs when `DOCQUERY_OPENAI_CONNECTION` is
set to a connection string.

The Service Bus emulator has no UI. To see what the relay published, peek the subscription (non-destructive)
with the connection string shown on the `servicebus` resource in the dashboard:

```bash
dotnet run tools/peek-servicebus.cs -- "<connection string>"
```

Arguments and conventions for tools are in [tools/README.md](tools/README.md).

## Query API

`DocQuery.Query.Api` (Aspire resource `query-api`) is the read side. `POST /queries` with a JSON body
`{ "question": "...", "documentId": "<optional guid>", "topK": <optional> }` embeds the question with the same
model as the chunks, fetches the nearest chunks from pgvector (cosine, HNSW index), and asks the chat model
(`AzureOpenAI:ChatDeployment`, `gpt-5-mini`) to answer from those passages only. The response is
`{ "answer": "...", "citations": [ { "number", "documentId", "fileName", "pageNumber", "position", "excerpt", "score" } ] }`;
`[n]` in the answer refers to citation `n`. With no embedded content the answer says so and the chat model is not
called. See ADR 0019.

- Retrieval size defaults to `Querying:DefaultTopK` and is capped by `Querying:MaxTopK`; an empty question or an
  out-of-range `topK` is a 400 with problem details.
- Azure OpenAI calls go through the same `Resilience:AzureOpenAI` pipeline as the embedding worker; an open circuit
  is a 503 with `Retry-After`.
- Questions are rate limited per client under `RateLimiting:Queries` (429 with `Retry-After`).
- The OpenAI connection string comes from Key Vault or the direct user secret exactly as for the embedding worker.
  The opt-in live test `AzureOpenAIChatSmokeTests` runs with the same `DOCQUERY_OPENAI_CONNECTION` variable.

Try it against the `query-api` endpoint shown in the dashboard (PowerShell; `curl` there is an alias for
`Invoke-WebRequest`):

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5291/queries -ContentType "application/json" -Body '{"question": "What is this document about?"}' | ConvertTo-Json -Depth 5
```

Or with curl proper:

```bash
curl -X POST http://localhost:5291/queries -H "Content-Type: application/json" -d "{\"question\": \"What is this document about?\"}"
```

## Web UI

`src/clients/docquery.web` (Aspire resource `web`) is a Vite React TypeScript app with one page: upload a PDF,
watch it move from Uploaded to Chunked to Embedded, then ask a question and read the answer with its citations.
It talks to the command API for uploads and to the query API for the document list and questions, using the URLs
the AppHost passes in `VITE_API_URL` and `VITE_QUERY_API_URL`. While any document is still processing the list is
polled every three seconds; idle, it makes no requests. See ADR 0020.

The AppHost also tells both APIs which browser origin to allow (`Cors__AllowedOrigins__0` from the Vite endpoint),
so the port Aspire picks for the dev server is always the one allowed. Outside Aspire the defaults in
`appsettings.json` allow `http://localhost:5173`.

Front-end tests use Vitest and Testing Library against a fake API client:

```bash
cd src/clients/docquery.web
npm test
```

`npm run build` type-checks and bundles; `npm run lint` runs oxlint; `npm audit` must report no known
vulnerabilities before front-end changes are committed.

## Build and test

```bash
dotnet build
dotnet test
```

Infrastructure tests start PostgreSQL and Azurite containers with Testcontainers. Without a running Docker
engine they are reported as skipped, not failed.

## Run locally

Aspire starts Azurite, PostgreSQL (pgvector image), the Service Bus emulator (with its SQL Edge sidecar), the
API, the outbox relay worker and the Vite dev server together and opens a dashboard with logs, traces and
metrics. Docker Desktop must be running for the emulators; the first start pulls several images. Azurite and
PostgreSQL use named Docker volumes and all emulators keep a persistent container lifetime, so uploaded documents
and database rows survive between sessions and migrations run only once. To start clean, stop Aspire and remove the `docquery` containers
and volumes (`docker ps -a`, `docker volume ls`). The Service Bus emulator reads its topics, subscriptions and
filters only when its container is created, so after changing them in the AppHost remove the `servicebus`
containers (`docker rm -f $(docker ps -aq --filter "name=servicebus")`) and start Aspire again. The generated PostgreSQL password lives in the AppHost user
secrets, so pgAdmin can connect with the credentials shown on the resource in the dashboard. Azurite's blob
port is pinned to 10000, so Azure Storage Explorer's built-in "Emulator - Default Ports" connection shows the
`documents` container without any configuration.

```bash
dotnet run --project src/DocQuery.AppHost
```

The UI can also be run on its own against APIs started separately (defaults: command API on 5290, query API on
5291, dev server on 5173):

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
