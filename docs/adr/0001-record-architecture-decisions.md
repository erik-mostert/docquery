# 0001. Record architecture decisions

Date: 2026-09-04

## Status

Accepted

## Context

DocQuery is a demonstration project for exam preparation (AI-200, AZ-104, AZ-305). Many design choices are made
for teaching value rather than pure pragmatism, and the reasoning behind them is easy to lose. Future readers,
including the author, need to see why a choice was made and what was rejected.

## Decision

Keep Architecture Decision Records in `docs/adr/`, one Markdown file per decision, numbered sequentially and
indexed in `docs/adr/README.md`. Use the MADR-style sections in `template.md`: Status, Context, Decision,
Consequences, Alternatives considered.

Accepted records are immutable. A reversed decision is recorded as a new ADR that supersedes the earlier one,
and the earlier one's status is updated to point at it.

## Consequences

- Every significant design choice costs a short document. That cost is deliberate.
- Code reviews can point at an ADR instead of re-arguing a settled question.
- The index doubles as a reading guide to the architecture.

## Alternatives considered

- **Design notes in the README.** Rejected: grows into an unstructured wall of text and loses the history of
  changed decisions.
- **Wiki or external document.** Rejected: separates decisions from the code they govern and from version control.
