# ADR-005: Approved-Only Translation Memory

## Status
Accepted

## Context
Reusing raw, unreviewed AI translations propagates hallucinations and glossary errors into future pages.

## Decision
Key `translation_memory_entries` by `(tenant_id, source_locale, target_locale, source_hash, glossary_version, content_type)` and enforce that `FindApprovedAsync` returns only `HUMAN_APPROVED` (`ApprovalStatus.Approved`) rows. AI outputs are stored strictly as `AI_GENERATED` + `Pending`.

## Consequences
- Zero unreviewed AI output is ever auto-promoted or reused across jobs.
- Optional Redis caching (`tenant:{tenantId}:tm:...`) accelerates approved lookups while falling back cleanly to PostgreSQL.
