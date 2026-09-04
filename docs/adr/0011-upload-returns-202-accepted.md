# 0011. Upload returns 202 Accepted

Date: 2026-09-04

## Status

Accepted

## Context

`POST /documents` stores the blob and the document row synchronously, then chunking and embedding happen
asynchronously in workers. Strictly, a `Document` resource is created, which argues for `201 Created` with a
`Location` header. But the resource is not useful until processing completes, and the read side that would
serve `Location` lives in the Query API, which does not exist yet and will be a separate host.

## Decision

Return `202 Accepted` with a JSON body `{ "documentId": "<guid>" }` and no `Location` header. The status
communicates "received, processing continues"; the id lets the client poll or subscribe later.

Validation failures return problem details: `400` for a missing, empty or non-PDF file (domain invariant),
`413` when the file exceeds the configured limit, `429` when rate limited (ADR 0008), `503` with `Retry-After`
when the storage circuit is open (ADR 0007).

## Consequences

- Clients must not assume the document is queryable immediately after a `202`.
- When the Query API gains a document-status endpoint, this decision should be revisited: either keep `202`
  and add `Location` pointing at that endpoint, or switch to `201`.
- OpenAPI describes the exact response set through typed results, so the UI client can be generated.

## Alternatives considered

- **201 Created with Location.** Deferred until a status endpoint exists.
- **200 OK.** Rejected: hides the fact that work is still in progress.
