# 0005. No authentication or authorization in the demo

Date: 2026-09-04

## Status

Accepted

## Context

DocQuery is a demonstration of a document-processing and RAG pipeline. Identity would add Microsoft Entra ID app
registrations, token validation, per-user document ownership and a sign-in flow in the UI. None of that is the
subject being demonstrated, and it would slow down every local run.

## Decision

Expose all endpoints without authentication or authorization. State this prominently in the README together with
what a production deployment must add.

## Consequences

- Anyone who can reach the API can upload and query documents. The system must never be deployed to a
  publicly reachable endpoint in this form.
- The Kubernetes deployment should keep the APIs on an internal ingress or a private cluster.
- Adding Entra ID later touches the API hosts and the UI, not the Domain or Application layers, because no
  domain concept depends on a user identity today. If per-user ownership becomes a requirement, that will be a
  new ADR because it changes the `Document` aggregate.

## Alternatives considered

- **Entra ID from the start.** Rejected: cost without demonstration value for the chosen exam topics.
- **API key header.** Rejected: gives a false sense of security and still needs secret management.
