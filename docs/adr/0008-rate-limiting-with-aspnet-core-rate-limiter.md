# 0008. Inbound rate limiting with the ASP.NET Core rate limiter

Date: 2026-09-04

## Status

Accepted

## Context

The upload endpoint accepts multi-megabyte bodies and fans out into blob storage, a database write and a
message. Without a limit, one misbehaving client can saturate the API pod's bandwidth and drive the downstream
circuit breaker (ADR 0007) open for everyone. The demo has no authentication (ADR 0005), so the only client
identity available is the network address.

## Decision

Use the built-in `Microsoft.AspNetCore.RateLimiting` middleware with a named policy per endpoint class:

- `UploadRateLimitPolicy` implements `IRateLimiterPolicy<string>`: a sliding window partitioned by client IP
  address (`"unknown"` when none), with permit limit, window, segments and queue length bound from
  `RateLimiting:Uploads`.
- Rejected requests get `429 Too Many Requests`, a `Retry-After` header (lease metadata when present, otherwise
  the window length) and a problem-details body.
- Only the upload endpoint opts in with `RequireRateLimiting`; health probes are never limited.

Polly's rate limiter strategy is built on the same `System.Threading.RateLimiting` primitives, but the ASP.NET
Core middleware is the idiomatic inbound placement and integrates with endpoint metadata and OpenAPI.

## Consequences

- Behind a Kubernetes ingress, `RemoteIpAddress` is the proxy unless forwarded headers are configured. The
  deployment step must enable `ForwardedHeadersMiddleware` with the ingress as a known proxy, or every client
  shares one bucket.
- Per-IP partitioning is per pod; with several replicas the effective limit is multiplied. Acceptable for a demo.
  A shared store (Redis) or ingress-level limiting is the production answer.
- Limits are configuration, so tests lower them to prove rejection without hundreds of requests.

## Alternatives considered

- **Polly rate limiter in the handler.** Rejected: the limit is a transport concern and should reject before
  the body is read.
- **Ingress / API gateway rate limiting only.** Not rejected for production, but the demo needs to show the
  application-level mechanism, and local runs have no ingress.
- **Token bucket.** Rejected for uploads: sliding window gives a clearer "N per minute" story; token bucket
  suits bursty small requests better.
