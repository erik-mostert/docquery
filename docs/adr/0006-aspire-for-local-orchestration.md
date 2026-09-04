# 0006. Aspire for local orchestration

Date: 2026-09-04

## Status

Accepted

## Context

The solution will have two APIs, two workers, a React front end, PostgreSQL, Azure Blob Storage and Azure
Service Bus. Running that locally by hand means a docker-compose file, a dozen connection strings, and no shared
view of logs and traces. Kubernetes is the production target, but a full cluster is too heavy for the inner
development loop.

## Decision

Use Aspire 13.5 for local orchestration:

- `src/DocQuery.AppHost` declares the application graph: every .NET host, the Vite front end, and later the
  emulators (Azurite, Service Bus emulator, PostgreSQL with pgvector). Aspire assigns ports, injects connection
  strings and service-discovery endpoints, and runs the dashboard for logs, traces and metrics.
- `src/DocQuery.ServiceDefaults` is referenced by every .NET host and wires OpenTelemetry, health checks
  (`/health` readiness, `/alive` liveness), service discovery and default `HttpClient` resilience. The
  health endpoints are mapped in every environment, not only Development, because Kubernetes probes need them.
- Kubernetes manifests are produced separately (later ADR); Aspire is the local model of the same graph.

## Consequences

- One command (`dotnet run --project src/DocQuery.AppHost`) starts everything with a dashboard.
- Container-backed resources need Docker Desktop running.
- Aspire's orchestrator (DCP) communicates with itself over TLS on loopback. Antivirus products that inspect
  local TLS traffic (Norton was observed doing this) break it with
  `x509: certificate signed by unknown authority`; `dcp.exe` and `dotnet.exe` must be excluded from TLS
  inspection on such machines. Documented in the README.
- Every host takes a dependency on ServiceDefaults, which is the intended single place for cross-cutting
  telemetry and resilience defaults.

## Alternatives considered

- **docker-compose.** Rejected: no dashboard, no automatic wiring of connection strings, and the .NET projects
  would still be run separately during development.
- **Tilt / Skaffold against a local cluster.** Rejected: heavier inner loop, and Aspire already models the graph
  we need for manifests.
- **No orchestration, run each project by hand.** Rejected: too many moving parts.
