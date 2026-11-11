---
name: optiflow-architecture
description: >-
  Use this skill when implementing any OptiFlow component: the diff engine,
  translation memory, merge logic, AI gateway, review workflow, or CMS adapter.
  Contains the full invariants, state machine transitions, merge rules, pipeline
  flow, API endpoints, and domain model from the design document.
---

# OptiFlow Architecture Reference

## 1. End-to-End Pipeline (9 Steps)

```
1. Ingest    → CMS event or manual sync → validate → deduplicate via webhook_inbox
2. Snapshot  → Store content version immutably
3. Diff      → Match blocks by stable ID → compare SHA-256 hashes
4. TM Lookup → Check translation memory for approved matches
5. AI        → Send remaining blocks to Gemma → structured JSON response
6. Validate  → Deterministic checks first, then optional semantic checks
7. Merge     → Preserve human edits, flag conflicts for reviewer
8. Review    → Side-by-side dashboard → reviewer approves or rejects
9. Draft     → On approval → CMS draft with ETag/version check
```

## 2. Domain Model

```
ContentItem → ContentVersion → ContentBlock → Translation
LocalizationJob → LocalizationJobBlock, AIInvocation, ReviewTask
```

### Value Objects (strongly typed)

| Type | Underlying | Purpose |
|---|---|---|
| `TenantId` | `Guid` | Tenant isolation key |
| `ContentKey` | `string` | External content identifier |
| `ContentVersionId` | `Guid` | Version identity |
| `BlockKey` | `string` | Stable block identifier |
| `LocaleCode` | `string` | Language/region code (e.g., `es-ES`) |
| `ContentHash` | `string` | SHA-256 hex of normalized text |
| `JobId` | `Guid` | Localization job identity |
| `IdempotencyKey` | `string` | Deduplication key |

## 3. Core Interfaces

```csharp
public interface IContentSource
{
    Task<ContentSnapshot> GetContentAsync(
        string contentKey, string locale, CancellationToken ct);
    Task<IReadOnlyList<ContentVersion>> GetVersionsAsync(
        string contentKey, CancellationToken ct);
    Task<DraftResult> CreateDraftAsync(
        DraftContent content, string expectedVersion, CancellationToken ct);
}

public interface IContentDiffEngine
{
    ContentDiffResult Compare(ContentSnapshot previous, ContentSnapshot current);
}

public interface ITranslationMemory
{
    Task<TranslationMemoryEntry?> FindApprovedAsync(
        TranslationMemoryKey key, CancellationToken ct);
    Task SaveApprovedAsync(
        TranslationMemoryEntry entry, CancellationToken ct);
}

public interface ILocalizationOrchestrator
{
    Task ProcessAsync(Guid localizationJobId, CancellationToken ct);
    Task<AnalysisResult> AnalyzeAsync(Guid contentItemId, CancellationToken ct);
}

public interface ILanguageModelClient
{
    Task<TranslationResult> TranslateAsync(
        TranslationRequest request, CancellationToken ct);
}
```

## 4. State Machine

```
QUEUED → SNAPSHOTTING → DIFFING → TRANSLATING → VALIDATING → AWAITING_REVIEW
                                                                    ↓
                                              APPROVED → CMS_DRAFT_CREATED
                                              REJECTED (terminal — never published)
```

### Full Transition Table

| From | Condition | To |
|---|---|---|
| `QUEUED` | Job claimed by worker | `SNAPSHOTTING` |
| `SNAPSHOTTING` | Snapshot captured | `DIFFING` |
| `DIFFING` | All blocks reused (TM + unchanged) | `AWAITING_REVIEW` |
| `DIFFING` | Some blocks need AI translation | `TRANSLATING` |
| `TRANSLATING` | All translations received | `VALIDATING` |
| `VALIDATING` | All checks passed | `AWAITING_REVIEW` |
| `VALIDATING` | Transient failure | `RETRY_WAIT` → `TRANSLATING` |
| `VALIDATING` | Quality below threshold | `NEEDS_MANUAL_REVIEW` |
| `AWAITING_REVIEW` | Reviewer approves | `APPROVED` |
| `AWAITING_REVIEW` | Reviewer rejects | `REJECTED` |
| `APPROVED` | CMS draft created successfully | `CMS_DRAFT_CREATED` |
| `APPROVED` | CMS version mismatch (ETag) | `STALE_CONFLICT` → `DIFFING` |
| `RETRY_WAIT` | Backoff elapsed | `TRANSLATING` |
| `* (max retries)` | All retries exhausted | `DEAD_LETTER` |
| `* (unrecoverable)` | Permission/config error | `FAILED` |

### State Rules
- `FAILED` is NOT automatically retryable — requires manual intervention.
- `REJECTED` can NEVER be published.
- `CMS_DRAFT_CREATED` means a draft exists — NOT that it is published.
- `DEAD_LETTER` requires explicit admin re-queue.

## 5. Merge Rules (Core Business Logic)

| Block Situation | Action |
|---|---|
| Source unchanged | Keep existing translation — NO AI call |
| Modified + approved TM hit | Reuse the TM translation |
| Modified + target machine-managed | Translate and replace |
| Source unchanged + target human-edited | **Preserve** (never overwrite) |
| Modified + target human-edited | **CONFLICT** → send to reviewer |
| Added block | TM lookup first, otherwise translate |
| Deleted block | Propose removal in the draft |

> [!CAUTION]
> Fuzzy/semantic matching is a **suggestion only**. It NEVER silently overwrites
> a human translation.

## 6. Nine Invariants

Every implementation MUST uphold these:

1. **Content snapshots are immutable** — never update an old version row.
2. **Never publish automatically** — only create drafts after human approval.
3. **Never overwrite a human-edited translation** without reviewer conflict
   resolution.
4. **Translation memory reuses APPROVED entries only** — AI output is never
   auto-approved.
5. **Duplicate CMS events never create duplicate jobs** — idempotent via
   `webhook_inbox` unique key.
6. **Check latest CMS version/ETag before creating a draft** — mismatch →
   `STALE_CONFLICT`.
7. **Validate every LLM output deterministically** — JSON schema, non-empty,
   HTML tags preserved, placeholders unchanged, glossary followed, no abnormal
   length growth, source version still current. Never trust model confidence.
8. **Logs contain IDs, hashes, redacted metadata** — never raw customer content.
9. **Fuzzy/semantic matching is a suggestion only** — never an automatic
   overwrite.

## 7. AI Layer Specification

### Input format (strict JSON):
```json
{
  "source_locale": "en",
  "target_locale": "es-ES",
  "brand": "premium-hotel",
  "content_type": "hero-title",
  "source_text": "Welcome to our luxury hotel",
  "glossary": [{ "term": "Luxury", "preferred_translation": "de lujo" }]
}
```

### Output format (strict JSON):
```json
{
  "translated_text": "Bienvenido a nuestro hotel de lujo",
  "confidence": 0.94
}
```

### Deterministic Validation (mandatory, every response):
- [ ] Valid JSON matching expected schema
- [ ] `translated_text` is non-empty
- [ ] HTML tags preserved (same tags, same order)
- [ ] Placeholders unchanged (e.g., `{name}`, `{{count}}`)
- [ ] Glossary terms followed
- [ ] No abnormal length growth (>3x source length)
- [ ] Source version still current (not stale)

### Semantic Validation (optional):
- Meaning preservation
- Tone consistency
- Fluency score

> Do NOT trust `confidence` as a calibrated quality score.

## 8. REST API Reference

| Method | Endpoint | Purpose | Response |
|---|---|---|---|
| POST | `/api/v1/cms/connections` | Create CMS connection | 201 |
| GET | `/api/v1/cms/connections` | List connections | 200 |
| POST | `/api/v1/content/sync` | Trigger content sync | 202 |
| GET | `/api/v1/content/{id}/versions` | List content versions | 200 |
| GET | `/api/v1/content/{id}/diff` | Compare versions | 200 |
| POST | `/api/v1/localization/jobs` | Create localization job | 202 |
| GET | `/api/v1/localization/jobs/{id}` | Get job status | 200 |
| GET | `/api/v1/localization/jobs/{id}/blocks` | Block-level results | 200 |
| GET | `/api/v1/reviews` | List pending reviews | 200 |
| POST | `/api/v1/reviews/{id}/approve` | Approve review | 200 |
| POST | `/api/v1/reviews/{id}/reject` | Reject review | 200 |
| GET | `/api/v1/analytics/costs` | Cost analytics | 200 |
| GET | `/api/v1/analytics/performance` | Performance metrics | 200 |
| POST | `/api/v1/webhooks/optimizely` | Receive CMS events | 200 |

## 9. Database Tables

| Table | Key Fields |
|---|---|
| `tenants` | id, name, slug, status |
| `cms_connections` | id, tenant_id, provider, base_url, credential_ref, status |
| `content_items` | id, tenant_id, connection_id, external_content_key, content_type, source_locale |
| `content_versions` | id, content_item_id, external_version_key, locale, version_number, content_hash, raw_payload (JSONB) |
| `content_blocks` | id, content_version_id, block_key, field_path, block_type, source_text, content_hash, ordinal |
| `localization_jobs` | id, tenant_id, content_item_id, source_version_id, target_locale, status, idempotency_key, priority, attempt_count, max_attempts, available_at, locked_by, locked_at, lease_expires_at, last_error_code |
| `localization_job_blocks` | id, job_id, block_key, source_hash, action_type, status, translated_text, translation_memory_id, validation_result (JSONB) |
| `translation_memory_entries` | id, tenant_id, source_locale, target_locale, source_hash, translated_text, provenance, approval_status, glossary_version, content_type |
| `glossary_terms` | id, tenant_id, source/target_locale, source_term, preferred_translation, forbidden_translation |
| `review_tasks` | id, job_id, reviewer_id, status, decision, comments, reviewed_at |
| `ai_invocations` | id, job_id, job_block_id, provider, model_name, prompt_version, input_tokens, output_tokens, latency_ms, estimated_cost, status |
| `audit_events` | id, tenant_id, actor_id, action, resource_type, resource_id, metadata (JSONB) |
| `webhook_inbox` | id, tenant_id, provider, external_event_id, payload_hash, processing_status, received_at, processed_at |
| `outbox_messages` | id, aggregate_id, event_type, payload, status, created_at, published_at |
