# 0017. Embeddings with Azure OpenAI through Microsoft.Extensions.AI, stored in pgvector

Date: 2026-09-06

## Status

Accepted

## Context

Chunks exist; the query step needs a vector per chunk to retrieve relevant passages. Decisions with the project
owner: Azure OpenAI is the only provider (no local model), `text-embedding-3-small` at 1536 dimensions, vectors
stored on the chunk row. The worker must reuse the consumer pattern from ADR 0015 and read its secret from the
Key Vault provisioned in ADR 0016.

## Decision

- **Abstraction.** The Application layer depends on `Microsoft.Extensions.AI`'s
  `IEmbeddingGenerator<string, Embedding<float>>` directly rather than on a home-grown interface; it is the
  provider-neutral abstraction the ecosystem converges on and tests use a fake implementation of it.
- **Provider.** Infrastructure builds an `AzureOpenAIClient` from `ConnectionStrings:openai`
  (`Endpoint=…;Key=…`; without a key, `DefaultAzureCredential` is used, which covers managed identity in Azure and
  `az login` locally) and exposes the deployment named in `AzureOpenAI:EmbeddingDeployment` as the generator. The
  Aspire client integration for OpenAI is preview-only, so the client is registered by hand. A missing connection
  string fails the host at startup with the key named.
- **Resilience.** The generator is wrapped with OpenTelemetry and a Polly pipeline (`Resilience:AzureOpenAI`,
  retry with exponential backoff and jitter, circuit breaker, timeout); the SDK's own retries are disabled so
  Polly is the single retry authority (ADR 0007). 429 throttling is the expected transient case.
- **Dimensions.** `DocumentChunk.EmbeddingDimensions = 1536` is the single source of truth: the domain validates
  every vector against it, the EF mapping derives `vector(1536)` from it, and the handler treats a provider
  returning another size as a permanent failure (a configuration error, not a transient one). Changing model or
  size is a migration plus re-embedding every chunk.
- **Storage.** `document_chunks.Embedding` is a nullable `vector(1536)` column via `Pgvector.EntityFrameworkCore`,
  with an HNSW index using `vector_cosine_ops`. The domain keeps `ReadOnlyMemory<float>?`; the pgvector `Vector`
  type is a value-converter detail. Similarity queries use raw SQL (`ORDER BY "Embedding" <=> @query`), which the
  persistence tests exercise, rather than LINQ translation over the converted property.
- **Handler.** `DocumentChunkedHandler` embeds only chunks without a vector, in batches of `Embedding:BatchSize`,
  then marks the document `Embedded` and raises `DocumentEmbeddedEvent` for the outbox. Redelivery is idempotent:
  documents past embedding are skipped, chunks already embedded are not resent. Failed documents keep their chunks
  and can be retried.
- **Secrets locally.** The worker loads Key Vault as a configuration source when `ConnectionStrings:keyvault`
  (the vault URI) is present, so `ConnectionStrings:openai` resolves from the vault through the developer's
  identity. Without a vault, a direct `ConnectionStrings:openai` user secret works too. The vault also holds the
  Azure storage, messaging and database connection strings, so it is inserted *below* the environment-variable
  sources (`ConfigurationSourceOrdering`): what the host injects wins, and the vault only fills gaps. Under Aspire
  the emulators therefore still serve storage, database and messaging; only OpenAI is remote.

## Consequences

- Every embedding call costs money; there is no offline mode. The unit tests use a fake generator and the only
  live test is opt-in via `DOCQUERY_OPENAI_CONNECTION`.
- `Pgvector.EntityFrameworkCore` 0.3.0 targets EF Core 9 and is used on EF Core 10; the container tests prove the
  mapping, migration and cosine query work.
- A partially embedded document (transient failure mid-batch) is left `Chunked` with some vectors saved only if
  a later save succeeds; the next delivery finishes the rest.
- The `embedding` subscription also receives `DocumentUploaded` and `DocumentEmbedded` copies, which are completed
  as unknown subjects (ADR 0015). Subject filters remain a follow-up.

## Alternatives considered

- **Own `ITextEmbedder` interface.** Rejected: duplicates `IEmbeddingGenerator` without adding anything.
- **Ollama locally, Azure OpenAI in the cloud.** Rejected: different vector dimensions per environment would
  make the column and the stored data environment-specific.
- **Separate `chunk_embeddings` table.** Rejected for now: one model, one vector per chunk; a join on every query
  for flexibility nobody needs yet.
- **Aspire `AddAzureOpenAIClient` integration.** Deferred until it leaves preview.
