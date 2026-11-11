# ADR-008: Optimistic Concurrency (ETag/Version Check) on CMS Drafts

## Status
Accepted

## Context
Master CMS content can change between job creation and human review completion, risking stale translations overwriting newer content structure.

## Decision
Capture `expectedVersion` in `ReviewTask` and pass it (`If-Match`) to `IContentSource.CreateDraftAsync`. On version mismatch, transition the job to `STALE_CONFLICT`, return `409 Conflict` to the Review UI, re-sync the latest CMS snapshot, and restart from `DIFFING`.

## Consequences
- Stale drafts are never written silently to the CMS.
- The React Review UI surfaces explicit `409 STALE_CONFLICT` guidance without losing audit history.
