# 0003. Command API streams PDF uploads to Blob Storage

Date: 2026-09-04

## Status

Accepted

## Context

A PDF must end up in Azure Blob Storage with a `DocumentUploaded` message on the event bus. Two well-known
patterns exist:

1. The API accepts the multipart upload, streams it to Blob Storage, records the document, and publishes.
2. The valet-key pattern: the API issues a short-lived, write-only SAS URL; the browser uploads straight to Blob
   Storage; an Event Grid `BlobCreated` event triggers an Azure Function that records the document and publishes.

The valet-key pattern is the textbook AZ-305 answer for large uploads, but it needs Event Grid, an Azure Function,
orphaned "pending" rows to clean up, and has no local Event Grid emulator. The project's stated priority is to keep
the demo simple.

## Decision

Use option 1. The Command API accepts `POST /documents` as multipart form data, streams the file to Blob Storage
under a deterministic name (`{documentId}.pdf`), persists the `Document` aggregate, and publishes
`DocumentUploaded` through the outbox.

Order of operations is blob first, then database. A failure between the two leaves an orphaned blob, which is
harmless and can be reconciled later; the reverse order would leave a database row pointing at a blob that does
not exist, which downstream workers would act on.

The Blob Storage change feed and any reconciliation function are deferred until embedding updates are addressed.

## Consequences

- One upload path, fully runnable locally against Azurite.
- The API pod carries upload bandwidth and needs request-size limits and rate limiting.
- Orphaned blobs are possible after partial failure. The deterministic blob name makes retries idempotent
  overwrites; a lifecycle rule or reconciliation sweep can remove true orphans.

## Alternatives considered

- **Valet-key / SAS direct upload with Event Grid and Azure Functions.** Rejected for now on simplicity grounds.
  Remains the recommended production pattern for large files and is a candidate for a later ADR.
- **Database first, then blob.** Rejected: leaves committed rows and outbox messages for missing blobs.
