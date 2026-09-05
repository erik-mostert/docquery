# 0015. Chunking worker: consumer pattern and structure-aware chunking

Date: 2026-09-05

## Status

Accepted

## Context

`DocumentUploaded` messages reach the `chunking` subscription (ADR 0014) and nothing consumes them. The chunking
worker is the first consumer, so it also fixes the consumer-side conventions the embedding worker will reuse. Two
requirements shaped the chunking itself: chunk sizes must be measured in the embedding model's tokens, and lists
in documents must not be split across chunks.

## Decision

### Consumer pattern
- **Routing by `Subject`.** `AddIntegrationMessageHandler<TMessage, THandler>()` registers a scoped handler and a
  `MessageRoute` (subject = contract type full name → CLR type → invoke delegate). `MessageDispatcher` looks the
  subject up in a frozen dictionary, deserializes with the shared `MessageSerialization.Options`, and invokes the
  handler in a fresh DI scope. No reflection at runtime; duplicate subjects fail at startup.
- **Settlement.** `ServiceBusSubscriptionListener` (`ServiceBusProcessor`, manual completion): handled → complete;
  **unknown subject → complete and log** (a consumer ignores what it does not handle; the topic carries every
  document event and subscriptions are unfiltered for now); `PermanentProcessingException` or unreadable JSON →
  dead-letter with reason and description; any other exception → abandon, so Service Bus redelivers until the
  subscription's `MaxDeliveryCount` (5) and then dead-letters.
- **Permanent versus transient.** The rule: if the input is immutable, the failure is permanent. A missing
  document, a missing blob, an unparseable PDF and a PDF with no extractable text will fail identically on every
  retry, so the handler marks the document `Failed` (with the reason), commits, and rethrows for dead-lettering.
  Database and network errors are left to redelivery.
- **Idempotency.** Delivery is at-least-once. The handler skips any document that is not in a chunkable state
  (`Uploaded` or `Failed`), which also protects documents that have moved past chunking. Re-chunking a `Failed`
  document replaces its chunks atomically with the status change.
- **Lock renewal.** Chunking a 50 MB PDF can exceed the one-minute lock; the processor renews for up to ten minutes
  (`Messaging:Subscription:MaxAutoLockRenewalDuration`).
- **Retries.** The publisher disables SDK retries in favour of Polly (ADR 0007); the consumer keeps the SDK's
  defaults for receive and settle because no Polly pipeline wraps that path. Both roles share one client
  registration per host.

### Chunking
- **Token-based sizing** with `Microsoft.ML.Tokenizers` (`o200k_base`), the encoding of current Azure OpenAI
  models, so every chunk is guaranteed under the embedding limit (`Chunking:MaxTokens`, default 500).
- **Structure first.** PdfPig's Docstrum page segmentation yields layout blocks. `BlockSegmenter` turns them into
  semantic blocks: runs of lines starting with a list marker (bullets, `1.`, `1)`, `(a)`, roman numerals) become one
  list block, a preceding line ending with `:` is its intro, wrapped item lines stay with their item; everything else
  is a paragraph. `TextChunker` packs whole blocks into chunks; a block that would overflow starts the next chunk;
  chunks never cross a page. Overlap repeats the previous block when it fits `OverlapTokens`, so it never starts
  mid-list.
- **Split only when unavoidable**, along the block's own structure: a list between items, a paragraph between
  sentences, and only as a last resort on token boundaries, slicing the original text by token offsets (never by
  decoding tokens, which can break a UTF-8 character in byte-level BPE).
- **Caveat.** PDF has no semantic markup; list detection is a heuristic over layout and leading characters.
  Scanned PDFs, multi-column layouts and tables may be misjudged. Word or HTML sources would give real structure
  behind the same extractor abstraction.

## Consequences

- The embedding worker needs only a handler and `AddIntegrationMessageHandler`; the transport code is shared.
- Every `DocumentChunked` message is also delivered to the `chunking` subscription and completed as unknown. A
  correlation filter on `Subject` per subscription is a small follow-up once a second consumer exists.
- Failed documents are visible in `documents.FailureReason` and the message in the subscription's dead-letter
  queue; there is no automatic retry of `Failed` documents yet.
- Extraction and chunking are CPU-bound and run inside the processor's concurrency limit (`MaxConcurrentCalls`).

## Alternatives considered

- **Fixed-size token windows only.** Rejected: splits lists and sentences arbitrarily.
- **Character-based sizing.** Rejected: token counts are what the model limits; characters only approximate them.
- **Semantic Kernel `TextChunker`.** Rejected: line/paragraph based, no list awareness, experimental API.
- **Dead-letter unknown subjects.** Rejected: with unfiltered subscriptions every successful chunking would produce
  a dead-lettered `DocumentChunked`.
- **`BackgroundService` for the listener.** Rejected: the processor owns its lifecycle; `IHostedService` plus
  `IAsyncDisposable` maps to start/stop/dispose without an idle `ExecuteAsync`.
