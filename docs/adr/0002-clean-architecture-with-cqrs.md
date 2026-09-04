# 0002. Clean Architecture with CQRS split into Command and Query APIs

Date: 2026-09-04

## Status

Accepted

## Context

The system has a clear write path (upload a PDF, chunk it, embed it) and a clear read path (answer natural
language questions over embeddings with a RAG pipeline). The two paths have different scaling profiles, different
dependencies, and different failure modes. The project also exists to demonstrate recognisable architectural
patterns, so the structure should be legible to someone who knows Clean Architecture and CQRS.

## Decision

Organise the backend as Clean Architecture layers, each its own project:

- `DocQuery.Domain`: entities, value objects, domain events. No dependencies.
- `DocQuery.Application`: commands, queries, handlers, and the interfaces they need (`IBlobStore`, `IEventBus`,
  repositories, unit of work). Depends only on Domain.
- `DocQuery.Contracts`: integration message contracts shared by publishers and subscribers.
- `DocQuery.Infrastructure` and `DocQuery.Infrastructure.Persistence`: implementations of the Application
  interfaces (Blob Storage, Service Bus, AI clients; EF Core with pgvector).
- Hosts: `DocQuery.Command.Api` (write side), `DocQuery.Query.Api` (read side), one worker project per
  background job (`Workers.Chunking`, `Workers.Embedding`).

Command and query hosts are separate deployable units so they scale and fail independently in Kubernetes.

## Consequences

- More projects than a minimal solution. Each has one reason to change, which matches the SOLID goal.
- Persistence is its own project so EF Core migrations and the pgvector schema are isolated from the other
  adapters, and hosts that do not need a database do not carry the EF dependency.
- Application code is testable with fakes; infrastructure is tested against emulators and containers.
- Workers are separate projects, not hosted services in one process, so each can be scaled as its own Deployment.

## Alternatives considered

- **Single API project with folders.** Rejected: hides the dependency direction and makes independent scaling of
  read and write paths impossible.
- **One worker project hosting both chunking and embedding.** Rejected: chunking is CPU/IO bound, embedding is
  rate-limited by the AI service; they need different replica counts.
- **A generic `Core` project alongside `Domain`.** Rejected: ambiguous boundary. Split into Domain and Application.
