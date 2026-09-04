# 0009. Hand-rolled command handlers and one class per endpoint

Date: 2026-09-04

## Status

Accepted

## Context

CQRS (ADR 0002) needs a way to route a request to a handler. The common .NET answer is MediatR, which since
version 13 is commercially licensed, or a similar mediator library. Minimal APIs, meanwhile, encourage putting
route handlers inline in `Program.cs`, which grows without bound and mixes unrelated endpoints.

## Decision

- Define `ICommandHandler<TCommand, TResult>` in the Application layer and register one implementation per
  command. Endpoints depend on the interface and never on a concrete handler or a mediator.
- Each endpoint is its own class implementing `IEndpoint.Map(IEndpointRouteBuilder)`. Implementations are
  discovered by assembly scan in `AddEndpoints` and mapped by `MapEndpoints`, so adding an endpoint never touches
  `Program.cs`.
- Cross-cutting behaviour (exception-to-problem-details mapping, rate limiting, CORS) is middleware or endpoint
  metadata, not handler decorators.

## Consequences

- No mediator dependency and no licence question. Pipeline behaviours (logging, validation) are added as
  explicit decorators on `ICommandHandler` when needed, which keeps them visible.
- Each endpoint class has one reason to change and can carry its own OpenAPI metadata, rate-limit policy and
  request-size limit.
- Handlers are trivially unit-testable with fakes; endpoints are tested through `WebApplicationFactory`.

## Alternatives considered

- **MediatR.** Rejected: licence cost and an extra abstraction for a handful of commands.
- **Wolverine / Brighter.** Deferred; they bundle messaging as well and may be revisited with the outbox
  (ADR 0004).
- **Controllers.** Rejected: heavier than needed and pulls in MVC conventions the demo does not use.
- **Inline route handlers in `Program.cs`.** Rejected: does not scale past two or three endpoints.
