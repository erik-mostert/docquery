# 0019. Query API answers questions with retrieval-augmented generation over pgvector, citing passages

Date: 2026-09-06

## Status

Accepted

## Context

Documents are uploaded, chunked and embedded (ADR 0015, ADR 0017). The read side promised by ADR 0002, a
separate `DocQuery.Query.Api`, has to answer natural-language questions over that corpus. The demo must show the
recognisable RAG pattern end to end, keep the answer grounded in the uploaded content, and make every claim
traceable to a passage, because an answer without a source is not useful for a document system.

Constraints: one chat deployment (`gpt-5-mini`, Global Standard, the model the subscription has quota for; the
name is configuration), the same embedding model as the chunks (a different model would put the question in a
different vector space), and the same resilience and rate-limiting expectations as the command API (ADR 0007,
ADR 0008).

## Decision

**Endpoint.** `POST /queries` with `{ question, documentId?, topK? }` returns `200 { answer, citations[] }`. The
answer is a complete string; there is no streaming in this version. Each citation carries its number, document
id, file name, page, chunk position, an excerpt and a cosine similarity score. `[n]` in the answer refers to
citation `n`.

**Pipeline** (`AskQuestionHandler` in Application, on Microsoft.Extensions.AI abstractions):

1. `Question` (Domain value object) enforces non-empty, trimmed, at most 2000 characters; a violation is a
   `DomainException` and therefore a 400.
2. The question is embedded with the same `IEmbeddingGenerator` the embedding worker uses. A vector of the wrong
   size is a configuration error and fails the request.
3. `IChunkSearch` returns the `topK` nearest chunks by cosine distance (`<=>`), joined with the document for its
   file name, optionally scoped to one document. Persistence implements it with raw SQL through EF Core's
   `SqlQuery`, served by the HNSW index. `topK` defaults to `Querying:DefaultTopK` (5) and is capped by
   `Querying:MaxTopK` (20).
4. With no match the handler returns a fixed "no content" answer and no citations without calling the chat
   model: there is nothing to ground an answer in, and the call would cost tokens for a guess.
5. Otherwise `RagPrompt` builds two messages: a fixed system instruction (answer only from the passages, cite
   with `[n]`, say when the passages do not answer, no outside knowledge, answer in the question's language) and
   one user message listing the passages as `[n] file, page p` followed by the text, then the question.
   `MaxOutputTokens` comes from `Querying:MaxAnswerTokens`. Temperature is left at the model default because the
   reasoning model family rejects other values.

**Infrastructure.** `IChatClient` is built from the shared `AzureOpenAIClient` (one client per host, embeddings
and chat both on it), wrapped with OpenTelemetry and a `ResilientChatClient` that applies the `azure-openai` Polly
pipeline to non-streaming calls; the SDK's own retries stay disabled (ADR 0007). Key Vault supplies the OpenAI
connection string the same way as for the embedding worker, below the environment variables.

**Host.** Shared web plumbing (endpoint discovery, health probes, problem-details exception handlers, CORS, the
per-client sliding-window rate limiter) moved from the command API into `DocQuery.Api.Common` so both APIs
compose from the same pieces. The query API adds its own `RateLimiting:Queries` policy.

## Consequences

- Every answer is traceable: citations are the retrieval result, not something the model was asked to invent,
  so they are correct even when the model's `[n]` references are sloppy.
- The chat call is the slow and expensive step; retrieval and validation happen first so bad requests and empty
  corpora cost nothing.
- Cosine similarity is reported as a score but no threshold is applied. Irrelevant passages can reach the model
  for off-topic questions; the system instruction tells it to say so. A threshold is a one-line change in the
  handler if it proves necessary.
- Streaming, conversation history, re-ranking and hybrid (keyword plus vector) search are deliberately absent.
  Each is an additive change behind the same endpoint or a new one.
- Retrieval reads chunks regardless of document status, so a document being re-embedded can surface stale
  passages briefly. Acceptable for the demo.

## Alternatives considered

- **Streaming the answer (Server-Sent Events).** Better perceived latency, but complicates error handling, rate
  limiting and the retry wrapper, and the UI step does not need it yet.
- **Semantic Kernel or a vector-store abstraction.** More moving parts than the pattern needs; the pipeline is
  five explicit steps and reads better as plain code on the MEAI abstractions the project already uses.
- **Letting the model return citations as structured output.** Model-produced citations can be fabricated;
  deriving them from retrieval keeps them true by construction.
- **EF LINQ for the vector query.** pgvector's operators have no LINQ translation with this EF version; raw SQL
  is confined to one class with a container test proving it.
