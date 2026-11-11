# Architecture Decision Records (ADRs)

This directory contains Architecture Decision Records for the OptiFlow project.

## Index

| ADR | Title | Status |
|---|---|---|
| [ADR-001](adr-001-modular-monolith.md) | Modular Monolith | Proposed |
| [ADR-002](adr-002-postgresql-queue.md) | PostgreSQL Job Queue | Proposed |
| [ADR-003](adr-003-immutable-snapshots.md) | Immutable Content Snapshots | Proposed |
| [ADR-004](adr-004-block-identity-sha256.md) | Stable Block Identity + SHA-256 | Proposed |
| [ADR-005](adr-005-translation-memory.md) | Translation Memory (Approved-Only) | Proposed |
| [ADR-006](adr-006-local-llm-adapter.md) | Local LLM via Adapter Pattern | Proposed |
| [ADR-007](adr-007-human-approval.md) | Human Approval Before CMS Draft | Proposed |
| [ADR-008](adr-008-optimistic-concurrency.md) | Optimistic Concurrency (ETag) | Proposed |
| [ADR-009](adr-009-opentelemetry.md) | OpenTelemetry Tracing | Proposed |

## Template

Each ADR follows this format:

```markdown
# ADR-NNN: Title

## Status
Proposed | Accepted | Deprecated | Superseded

## Context
What is the issue that we're seeing that is motivating this decision?

## Decision
What is the change that we're proposing and/or doing?

## Consequences
What becomes easier or more difficult to do because of this change?
```
