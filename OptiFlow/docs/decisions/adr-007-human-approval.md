# ADR-007: Human Approval Before CMS Draft Creation

## Status
Accepted

## Context
Enterprise CMS workflows require editorial review before localized changes reach Optimizely CMS, and manual human translations must never be silently overwritten.

## Decision
Never expose or invoke a CMS publish endpoint. Require explicit reviewer approval (`POST /api/v1/reviews/{id}/approve`) before calling `IContentSource.CreateDraftAsync`. Route modified human-edited blocks to `ConflictRequiresReview` and forbid publishing `Rejected` jobs.

## Consequences
- Human edits are preserved 100% when source blocks are unchanged and explicitly surfaced during conflicts.
- Validation failures block draft creation until resolved by a reviewer.
