# 0007. Polly retry and circuit breaker around outbound dependencies

Date: 2026-09-04

## Status

Accepted

## Context

The Command API depends on Azure Blob Storage, later on Azure Service Bus and PostgreSQL. Each can fail
transiently (throttling, network blips) or hard (outage). Without a policy, a transient failure surfaces as a
500 to the user, and a hard outage ties up request threads in timeouts and hammers the failing dependency.
The project owner wants Polly with a circuit breaker and exponential backoff with jitter, and the exams
(AZ-305) treat retry, circuit breaker and bulkhead as core reliability patterns.

## Decision

Use Polly v8 through `Microsoft.Extensions.Resilience`:

- Named resilience pipelines are registered with `AddResiliencePipeline` in `DocQuery.Infrastructure`, one per
  outbound dependency (first: `"blob-storage"`). Each pipeline is: retry (exponential backoff, jitter, bounded
  attempts) wrapping circuit breaker (failure ratio over a sampling window, minimum throughput, break duration)
  wrapping a per-attempt timeout. Settings are bound from configuration so they can be tuned per environment.
- The pipeline is applied with the decorator pattern: `ResilientBlobStore : IBlobStore` wraps the raw adapter.
  The Application layer and its handlers never see Polly. The decorator rewinds the upload stream before each
  attempt so retries resend the whole file.
- An open circuit surfaces as `BrokenCircuitException`, mapped by the API to `503 Service Unavailable` with a
  `Retry-After` header, so callers back off instead of retrying immediately.
- Database retries use the EF Core / Npgsql execution strategy rather than Polly, because the execution
  strategy understands transaction boundaries.
- Outbound `HttpClient` calls get the Aspire ServiceDefaults standard resilience handler, which is the same
  Polly stack with default settings.

## Consequences

- Transient blob failures are retried with backoff; an outage trips the breaker and fails fast for the break
  duration.
- Retrying a multipart upload requires a seekable stream. ASP.NET Core form files are buffered and seekable;
  the decorator rejects non-seekable streams rather than silently sending a partial retry.
- Retry plus circuit breaker plus the Azure SDK's own retry policy can multiply attempts. The Azure SDK retry
  will be reduced or disabled when the real adapter is added so Polly is the single retry authority.
- Every new outbound adapter should get its own named pipeline and decorator rather than sharing one.

## Alternatives considered

- **Azure SDK built-in retries only.** Rejected: no circuit breaker, no cross-SDK consistency, and no way to
  demonstrate the pattern explicitly.
- **Polly inside the handler.** Rejected: leaks an infrastructure concern into the Application layer and makes
  handler tests slower and noisier.
- **A generic "resilient everything" wrapper.** Rejected: dependencies need different thresholds; a shared
  breaker would let one failing dependency block the others.
