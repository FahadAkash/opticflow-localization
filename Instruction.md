# OptiFlow: Summary, HLD, LLD and Architecture Prompt

## 1. Summary

**What it is:** OptiFlow is an incremental localization engine for Optimizely CMS. It is a specialized processing service beside the CMS, and it does not replace the CMS, Opal, or the approval engine.

**The problem:** According to the doc, an Optimizely Opal feedback request (dated 16 July 2026) says a small change to master content forces a full-page retranslation. That wastes AI credits, risks overwriting manual translation edits, and slows maintenance. The 80–90% credit savings figure is the customer's projection, not a verified result.

**The solution:** Detect changes per block, reuse approved translations, translate only what changed with a local LLM, validate the output, and merge without touching human-edited text. A reviewer approves the result, which then goes to the CMS as a **draft**. There is no auto-publish.

**Key decisions**

| Area | Decision | Why |
|---|---|---|
| Backend | ASP.NET Core (.NET 8) | `Optimizely.Opal.Tools` SDK, so it can later be exposed as Opal tools |
| Architecture | Modular monolith, API and Worker as separate processes | Production-style separation without 10 microservices |
| Queue | PostgreSQL job queue (`FOR UPDATE SKIP LOCKED`) | Queue and business state commit in one transaction |
| AI | Gemma via LM Studio behind an adapter | Local and cheap; swappable later |
| Frontend | React + TypeScript | Operations and review dashboard |
| Cache | Redis | Repeated lookups |
| Observability | OpenTelemetry + Grafana | End-to-end traces and metrics |
| Go | Not now | Only extract a Go service if benchmarks justify it |

**End-to-end flow (9 steps)**

1. **Ingest:** a CMS event or manual sync arrives. It is validated and deduplicated via `webhook_inbox`.
2. **Snapshot:** the content version is stored immutably.
3. **Diff:** blocks are matched by stable ID (falling back to field path) and compared by SHA-256 hash of normalized text.
4. **Translation memory:** changed blocks are looked up, and approved matches are reused.
5. **AI:** the remaining blocks go to Gemma, which must return structured JSON.
6. **Validate:** deterministic checks run first, then optional semantic checks.
7. **Merge:** human edits are preserved, and conflicts go to a reviewer.
8. **Review:** a side-by-side dashboard shows original, previous translation, new AI translation, and quality checks.
9. **Draft:** on approval, a draft is created in the CMS with an ETag/version check.

## 2. Requirements

| Priority | Requirements |
|---|---|
| **P0 (FR-01 to 12)** | CMS connection/auth, content ingestion, immutable snapshots, block extraction, block-level diff, translation memory lookup, local LLM translation, structured output validation, human-edit preservation, review/approval, CMS draft creation, job status/history |
| **P1 (FR-13 to 16)** | Token/cost tracking, quality analytics, retry of failed jobs, Opal custom tools |
| **P2 (FR-17)** | Multi-tenant workspace management (skip for now) |

**Non-functional targets** (your own engineering targets, not Optimizely SLAs):

| Category | Target |
|---|---|
| Performance | API p95 under 300 ms (excluding external calls) |
| Availability | 99.5% design target |
| Idempotency | Unique key `tenant_id + provider + external_event_id` |
| Data integrity | 100% preservation of unchanged blocks in automated tests |
| Concurrency | Bounded worker concurrency, stateless workers |
| Security | Credentials encrypted at rest, tenant scope enforced on every query |
| Recoverability | Expired leases are reclaimed after a worker crash |
| Consistency | Optimistic concurrency (ETag/version) on CMS writes |
| AI reliability | Invalid structured output is rejected and retried safely |
| Observability | Trace IDs across API, worker, and AI |

Retry policy: attempts at 0 s, 2 s, 4 s, 8 s (exponential backoff), then `DEAD_LETTER`. Validation and permission errors are never retried blindly.

## 3. HLD (High-Level Design)

```
 [React Ops Dashboard] --REST/SignalR--> +----------------------------+
 [Optimizely CMS SaaS] --events/sync---> |  OptiFlow.Api (ASP.NET)    |
 [Optimizely Opal] ---tool calls------>  |  Auth, rate limit, webhook |
        (via OptiFlow.Opal)              |  inbox, controllers        |
                                         +-------------+--------------+
                                                       | persist event + job (1 tx), return 202
                                                       v
                          +-------------------------------------------------+
                          | PostgreSQL (data + job queue)     Redis (cache) |
                          +--------------------+----------------------------+
                                               ^ claim job (SKIP LOCKED + lease)
                                               |
                                 +-------------+--------------+
                                 |   OptiFlow.Worker          |
                                 |   Localization Orchestrator|
                                 +-------------+--------------+
   Version Mgmt -> Diff Engine -> Translation Memory -> AI Gateway -> Evaluation -> Merge -> Review task
                                                          |
                                                  LM Studio -> Gemma

   Review approved -> CMS Adapter -> (ETag check) -> CMS draft created

   Cross-cutting: OpenTelemetry -> Grafana, audit events, retry/recovery
```

**Layer responsibilities**

| Component | Responsibility |
|---|---|
| API Gateway | External requests, auth, rate limiting |
| Content Ingestion | Receives content from the CMS |
| Version Management | Immutable snapshots |
| Diff Engine | Identifies changed blocks |
| Translation Memory | Reuses approved translations |
| Orchestrator | Controls the workflow |
| AI Gateway | Model calls, timeout, retry, token tracking |
| Evaluation | Validates translations |
| Review Workflow | Human approval |
| CMS Adapter | Creates the reviewed draft in the CMS |
| Worker | Processes background jobs |
| Observability | Logs, traces, metrics |

**Why async:** If 100 content updates arrive at once, LM Studio can't answer inside an HTTP request. So the API persists the event and job, returns `202 Accepted`, and the worker processes it. The dashboard polls or uses SignalR for status.

**CMS integration boundary:** The first target is the Optimizely **CMS SaaS Content Management API**. There are three ingestion methods, so no single event source is a point of failure:

| Method | Use |
|---|---|
| Manual sync | First implementation and debugging |
| CMS integration event | Supported environments, per-content changes |
| Graph webhook | Bulk-completed and expiry notifications only. Don't treat it as a per-save webhook. |

CMS-specific code lives behind `IContentSource`, with `OptimizelySaaSContentSource` (real), `OptimizelyCms12ContentSource` (future), and `LocalTestContentSource` (tests only).

## 4. LLD (Low-Level Design)

### 4.1 Domain model

```
ContentItem -> ContentVersion -> ContentBlock -> Translation
LocalizationJob -> LocalizationJobBlock, AIInvocation, ReviewTask
```

Strongly typed value objects: `TenantId`, `ContentKey`, `ContentVersionId`, `BlockKey`, `LocaleCode`, `ContentHash`, `JobId`, `IdempotencyKey`.

### 4.2 Core interfaces

```csharp
public interface IContentSource
{
    Task<ContentSnapshot> GetContentAsync(
        string contentKey, string locale, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentVersion>> GetVersionsAsync(
        string contentKey, CancellationToken cancellationToken);

    Task<DraftResult> CreateDraftAsync(
        DraftContent content, string expectedVersion, CancellationToken cancellationToken);
}

public interface IContentDiffEngine
{
    ContentDiffResult Compare(ContentSnapshot previous, ContentSnapshot current);
}

public interface ITranslationMemory
{
    Task<TranslationMemoryEntry?> FindApprovedAsync(
        TranslationMemoryKey key, CancellationToken cancellationToken);

    Task SaveApprovedAsync(
        TranslationMemoryEntry entry, CancellationToken cancellationToken);
}

public interface ILocalizationOrchestrator
{
    Task ProcessAsync(Guid localizationJobId, CancellationToken cancellationToken);
}

public interface ILanguageModelClient
{
    Task<TranslationResult> TranslateAsync(
        TranslationRequest request, CancellationToken cancellationToken);
}
```

### 4.3 Diff Engine

1. Extract blocks from both snapshots, each with a stable `block_key`.
2. Normalize whitespace consistently, then compute the SHA-256 `content_hash`.
3. Match blocks by identity (CMS block ID first, field path as fallback), so reordering isn't mistaken for new blocks.
4. Compare hashes. The result per block is `unchanged`, `modified`, `added`, or `deleted`.
5. Semantic similarity is only a suggestion. It never silently overwrites a human translation.

### 4.4 Translation Memory

- **Lookup key:** tenant + source locale + target locale + source content hash + glossary version + content type.
- Only **approved** entries are reused. AI output is never auto-saved as approved.
- **Provenance:** `AI_GENERATED`, `HUMAN_EDITED`, `HUMAN_APPROVED`.

### 4.5 Merge rules (the core business logic)

| Block situation | Action |
|---|---|
| Source unchanged | Keep the existing translation, no AI call |
| Modified, approved TM hit | Reuse the TM translation |
| Modified, target machine-managed | Translate and replace |
| Source unchanged, target human-edited | Preserve |
| Modified, target human-edited | **Conflict, send to reviewer** |
| Added | TM lookup, otherwise translate |
| Deleted | Propose removal in the draft (my suggestion, the doc doesn't specify) |

### 4.6 AI layer

Model input and output (strict JSON):

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

```json
{ "translated_text": "Bienvenido a nuestro hotel de lujo", "confidence": 0.94 }
```

Don't treat the model's self-reported `confidence` as a calibrated quality score.

- **Pipeline:** changed block → context builder → prompt template → LM Studio (OpenAI-compatible API) → JSON parser → schema check → bounded retry → deterministic validation → store result and metrics.
- **Deterministic validation (mandatory):** JSON schema, non-empty output, HTML tags preserved, placeholders unchanged, glossary followed, no abnormal length growth, source version still current.
- **Semantic validation (optional):** meaning, tone, fluency. Use a small model-based check or human review, and don't force a second LLM call if the local model is slow.
- **Local strategy** (doc assumes RTX 3060 Ti 8 GB, 32 GB RAM): start with a 4B quantized model, small context and batches, concurrency 1 (raise only after benchmarking), request timeouts, cancellation, warm-up, and token tracking.

### 4.7 Job queue

```sql
WITH next_job AS (
    SELECT id
    FROM localization_jobs
    WHERE (
            (status = 'QUEUED' AND available_at <= NOW())
         OR (status = 'PROCESSING' AND lease_expires_at < NOW())
          )
      AND attempt_count < max_attempts
    ORDER BY priority DESC, created_at ASC
    FOR UPDATE SKIP LOCKED
    LIMIT 1
)
UPDATE localization_jobs j
SET status           = 'PROCESSING',
    locked_by        = @worker_id,
    locked_at        = NOW(),
    lease_expires_at = NOW() + INTERVAL '5 minutes',
    attempt_count    = j.attempt_count + 1
FROM next_job
WHERE j.id = next_job.id
RETURNING j.*;
```

`SKIP LOCKED` prevents two workers claiming the same job, but it does **not** make the external AI call exactly-once. You still need idempotent result storage and duplicate-request handling.

### 4.8 State machine

States: `QUEUED → SNAPSHOTTING → DIFFING → TRANSLATING → VALIDATING → AWAITING_REVIEW → APPROVED / REJECTED → CMS_DRAFT_CREATED`, plus `RETRY_WAIT`, `NEEDS_MANUAL_REVIEW`, `STALE_CONFLICT`, `FAILED`.

| From | Condition | To |
|---|---|---|
| DIFFING | All blocks reused | AWAITING_REVIEW |
| DIFFING | Blocks need AI | TRANSLATING |
| VALIDATING | Passed | AWAITING_REVIEW |
| VALIDATING | Transient failure | RETRY_WAIT, then back to TRANSLATING |
| VALIDATING | Quality failure | NEEDS_MANUAL_REVIEW |
| APPROVED | CMS version mismatch | STALE_CONFLICT, re-fetch latest, back to DIFFING |

Rules: `FAILED` isn't automatically retryable. `REJECTED` can never be published. `CMS_DRAFT_CREATED` means a draft exists, not that it's published.

### 4.9 Database (PostgreSQL)

| Table | Key fields |
|---|---|
| `tenants` | id, name, slug, status, created_at |
| `cms_connections` | id, tenant_id, provider, base_url, credential_ref, status |
| `content_items` | id, tenant_id, connection_id, external_content_key, content_type, source_locale, latest_version_id |
| `content_versions` | id, content_item_id, external_version_key, locale, version_number, content_hash, raw_payload (JSONB), captured_at |
| `content_blocks` | id, content_version_id, block_key, field_path, block_type, source_text, content_hash, ordinal |
| `localization_jobs` | id, tenant_id, content_item_id, source_version_id, target_locale, status, idempotency_key, priority, attempt_count, max_attempts, available_at, locked_by, locked_at, lease_expires_at, last_error_code, created_at, completed_at |
| `localization_job_blocks` | id, job_id, block_key, source_hash, action_type, status, translated_text, translation_memory_id, validation_result (JSONB) |
| `translation_memory_entries` | id, tenant_id, source_locale, target_locale, source_hash, translated_text, provenance, approval_status, glossary_version, content_type |
| `glossary_terms` | id, tenant_id, source/target_locale, source_term, preferred_translation, forbidden_translation |
| `review_tasks` | id, job_id, reviewer_id, status, decision, comments, reviewed_at |
| `ai_invocations` | id, job_id, job_block_id, provider, model_name, prompt_version, input_tokens, output_tokens, latency_ms, estimated_cost, status |
| `audit_events` | id, tenant_id, actor_id, action, resource_type, resource_id, metadata (JSONB) |
| `webhook_inbox` | id, tenant_id, provider, external_event_id, payload_hash, processing_status, received_at, processed_at |
| `outbox_messages` | id, aggregate_id, event_type, payload, status, created_at, published_at |

```
content_items:      UNIQUE(tenant_id, connection_id, external_content_key)
content_versions:   UNIQUE(content_item_id, external_version_key, locale)
content_blocks:     UNIQUE(content_version_id, block_key); INDEX(content_version_id, content_hash)
translation_memory: UNIQUE(tenant_id, source_locale, target_locale, source_hash, glossary_version, content_type)
webhook_inbox:      UNIQUE(tenant_id, provider, external_event_id)
```

Design rules:
- JSONB only for raw payloads, validation breakdowns, and audit metadata. Queryable fields stay as real columns.
- Snapshots are immutable: new version means a new row.
- Every tenant-owned table has `tenant_id`, with composite foreign keys and Row-Level Security where appropriate.
- Don't keep raw prompts or outputs longer than needed. Logs hold IDs, hashes, and redacted metadata, not content.

### 4.10 REST API (resource-oriented, long operations async)

| Method | Endpoint | Purpose |
|---|---|---|
| POST / GET | `/api/v1/cms/connections` | Create / list connections |
| POST | `/api/v1/content/sync` | Trigger sync |
| GET | `/api/v1/content/{id}/versions` | List versions |
| GET | `/api/v1/content/{id}/diff` | Compare versions |
| POST | `/api/v1/localization/jobs` | Create job (returns 202) |
| GET | `/api/v1/localization/jobs/{id}` | Job status |
| GET | `/api/v1/localization/jobs/{id}/blocks` | Block-level results |
| GET | `/api/v1/reviews` | Pending reviews |
| POST | `/api/v1/reviews/{id}/approve` or `/reject` | Decision |
| GET | `/api/v1/analytics/costs` / `/performance` | Usage and metrics |
| POST | `/api/v1/webhooks/optimizely` | Receive supported events |

```json
// POST /api/v1/localization/jobs
{ "content_id": "content-001", "source_version_id": "version-13", "target_locales": ["es-ES", "fr-FR"] }

// 202 Accepted
{ "job_id": "job-123", "status": "QUEUED", "target_locales": ["es-ES", "fr-FR"], "created_at": "2026-11-07T10:00:00Z" }
```

### 4.11 Solution structure

```
OptiFlow/
├── src/
│   ├── OptiFlow.Api/            Controllers, Middleware, Authentication, Filters
│   ├── OptiFlow.Application/    Content, Localization, Review, Analytics, Abstractions
│   ├── OptiFlow.Domain/         Content, Localization, TranslationMemory, Review, Events, ValueObjects
│   ├── OptiFlow.Infrastructure/ Persistence, CMS/Optimizely, AI (LMStudio, Prompts, Evaluation), Cache, Messaging
│   ├── OptiFlow.Worker/         Jobs, Scheduling
│   └── OptiFlow.Opal/           Tools, Parameters
├── frontend/react-dashboard/
├── tests/                       Unit, Integration, Architecture, Performance
├── infrastructure/              docker, grafana, otel
├── docs/                        architecture, decisions (ADRs), api, benchmarks
└── docker-compose.yml
```

Dependency direction: `Domain ← Application ← Infrastructure`, with Api, Worker, and Opal depending on Application. Domain must not reference any infrastructure package.

### 4.12 Opal tools (P1)

| Tool | Does |
|---|---|
| `analyze-content-changes` | Identifies changed blocks |
| `translate-content-delta` | Translates changed blocks |
| `get-localization-report` | Cost and quality report |

Tools only call application services. They never write to the database or the CMS directly. Calling it a real Opal integration needs a live Opal environment and authorization, not just a local endpoint.

## 5. Delivery plan

**Dashboard (4 screens):** Content Intelligence (modified/unchanged/added/deleted blocks), Localization Operations (queued/processing/failed jobs), Translation Review, Cost & Performance.

**Headline metrics:** Translation Reuse Rate, AI Credit Reduction (full-page baseline vs. incremental tokens), Content Preservation Rate, Processing Latency (p50/p95/p99), Job Success Rate. For cost, compare against the full-page baseline and against the estimated cloud LLM cost vs. local operating cost (electricity, hardware, time).

**Tests:** diff engine, TM reuse, human-edit preservation, idempotency, worker crash recovery, CMS draft/version conflicts, invalid-JSON rejection, load. The flagship test uses 100 blocks with 3 changed, and expects:
- Zero AI calls for the 97 unchanged blocks.
- The 3 changed blocks processed.
- Approved human edits preserved.
- No CMS draft created if validation fails.

**Milestones:**

| M1 | CMS integration + snapshots |
|---|---|
| M2 | Version-aware diff engine |
| M3 | Translation memory |
| M4 | Local AI processing |
| M5 | Human-edit-safe merge |
| M6 | Review + CMS draft |
| M7 | Benchmarks + observability |

**ADRs to write:** modular monolith, PostgreSQL queue, immutable snapshots, stable block identity + SHA-256, translation memory, local LLM via adapter, human approval before draft, optimistic concurrency, OpenTelemetry.

**Not building:** a full CMS, a multi-agent platform, an enterprise IdP, multi-cloud or Kafka, multi-region, auto-publish, or an Opal replacement.

## 6. Architecture prompt (paste-ready)

Paste this into Claude Code, a project's custom instructions, or an `AGENT.md`. Change only `<current_task>` per milestone.

````
<role>
You are a senior .NET and distributed-systems engineer pair-programming with me on OptiFlow. You value correctness, small verifiable steps, and tests that prove design decisions.
</role>

<objective>
Build OptiFlow: an incremental localization engine for Optimizely CMS. It detects changed content blocks, reuses approved translations, translates only modified blocks with a local LLM, validates output, merges without overwriting human-edited translations, gets reviewer approval, and creates a CMS DRAFT (never publishes).
</objective>

<context>
Problem: small master-content edits force full-page retranslation, wasting AI credits and risking loss of manual edits. OptiFlow is a specialized processing service beside the CMS. It does NOT replace the CMS, Opal, or the CMS approval engine.
</context>

<tech_stack>
Backend: ASP.NET Core (.NET 8), Worker Service. Opal: Optimizely.Opal.Tools (P1).
DB/queue: PostgreSQL (job queue via FOR UPDATE SKIP LOCKED + lease). Cache: Redis (optional).
AI: Gemma via LM Studio (OpenAI-compatible API) behind ILanguageModelClient.
Frontend: React + TypeScript. Observability: OpenTelemetry + Grafana.
CMS target: Optimizely CMS SaaS Content Management API.
</tech_stack>

<architecture>
Modular monolith + async worker. Projects: Api, Application, Domain, Infrastructure, Worker, Opal.
Dependencies: Domain <- Application <- Infrastructure; Api/Worker/Opal depend on Application.
Domain references no infrastructure packages. CMS-specific code lives only behind IContentSource.
API persists event + job in one transaction, returns 202 Accepted; the Worker does the processing.
</architecture>

<domain_model>
ContentItem -> ContentVersion -> ContentBlock -> Translation.
LocalizationJob -> LocalizationJobBlock, AIInvocation, ReviewTask.
Strongly typed value objects: TenantId, ContentKey, ContentVersionId, BlockKey, LocaleCode, ContentHash, JobId, IdempotencyKey.
</domain_model>

<core_interfaces>
IContentSource: GetContentAsync, GetVersionsAsync, CreateDraftAsync(content, expectedVersion).
IContentDiffEngine: Compare(previous, current) -> block results (unchanged/modified/added/deleted).
ITranslationMemory: FindApprovedAsync(key), SaveApprovedAsync(entry).
ILocalizationOrchestrator: ProcessAsync(jobId) (plus AnalyzeAsync for Opal tools).
ILanguageModelClient: TranslateAsync(request) returning strict JSON.
</core_interfaces>

<pipeline>
Load job -> load source version -> diff -> TM lookup -> create translation tasks -> dispatch AI (bounded concurrency) -> validate -> merge -> create review task -> on approval, CMS draft with ETag/version check.
</pipeline>

<merge_rules>
Unchanged source: keep existing translation, no AI call.
Modified + approved TM hit: reuse.
Modified + machine-managed target: translate and replace.
Unchanged source + human-edited target: preserve.
Modified + human-edited target: conflict, send to reviewer.
Added: TM lookup, else translate. Deleted: propose removal in draft.
</merge_rules>

<data_model>
Tables: tenants, cms_connections, content_items, content_versions (immutable), content_blocks, localization_jobs (priority, attempt_count, max_attempts, available_at, locked_by, locked_at, lease_expires_at, last_error_code), localization_job_blocks, translation_memory_entries (provenance: AI_GENERATED | HUMAN_EDITED | HUMAN_APPROVED; approval_status), glossary_terms, review_tasks, ai_invocations, audit_events, webhook_inbox (UNIQUE tenant_id + provider + external_event_id), outbox_messages.
Every tenant-owned table has tenant_id; every query is tenant-scoped server-side.
JSONB only for raw payloads, validation results, audit metadata.
</data_model>

<state_machine>
QUEUED, SNAPSHOTTING, DIFFING, TRANSLATING, VALIDATING, RETRY_WAIT, NEEDS_MANUAL_REVIEW, AWAITING_REVIEW, APPROVED, REJECTED, CMS_DRAFT_CREATED, STALE_CONFLICT, FAILED, DEAD_LETTER.
FAILED is not auto-retryable. STALE_CONFLICT re-fetches latest CMS version. REJECTED is never published. CMS_DRAFT_CREATED does not mean published.
Retry only transient errors: 0s, 2s, 4s, 8s, then DEAD_LETTER.
</state_machine>

<api>
POST /api/v1/cms/connections, GET /api/v1/cms/connections, POST /api/v1/content/sync, GET /api/v1/content/{id}/versions, GET /api/v1/content/{id}/diff, POST /api/v1/localization/jobs (202), GET /api/v1/localization/jobs/{id}, GET /api/v1/localization/jobs/{id}/blocks, GET /api/v1/reviews, POST /api/v1/reviews/{id}/approve|reject, GET /api/v1/analytics/costs|performance, POST /api/v1/webhooks/optimizely.
</api>

<invariants>
1. Content snapshots are immutable; never update an old version.
2. Never publish automatically; only create drafts after human approval.
3. Never overwrite a human-edited translation without reviewer conflict resolution.
4. Translation memory reuses APPROVED entries only; AI output is never auto-approved.
5. Duplicate CMS events never create duplicate jobs (idempotent).
6. Check latest CMS version/ETag before creating a draft; mismatch -> STALE_CONFLICT.
7. Validate every LLM output deterministically (JSON schema, non-empty, HTML tags, placeholders, glossary, length, source still current). Do not trust model confidence.
8. Logs contain IDs, hashes, redacted metadata, never raw customer content.
9. Fuzzy/semantic matching is a suggestion only, never an automatic overwrite.
</invariants>

<non_functional>
API p95 under 300 ms excluding external calls; 100% unchanged-block preservation in tests; stateless horizontally scalable workers; recover expired leases after crash; end-to-end trace IDs; encrypted credentials.
</non_functional>

<out_of_scope>
Full CMS, multi-agent platform, enterprise IdP, Kafka, multi-cloud/region, auto-publish, Opal replacement, extra microservices (no Go service until benchmarks justify it).
</out_of_scope>

<testing>
Unit: diff engine, TM, preservation, validators. Integration: idempotency, crash recovery, CMS draft + version conflict, invalid-JSON rejection. Flagship test: 100 blocks, 3 changed -> 0 AI calls for 97 blocks, 3 processed, human edits preserved, no draft if validation fails.
</testing>

<working_rules>
- Deliver complete, copy-paste-ready files. Never truncate or use placeholders like "rest of code here".
- Start each file with its path. List new packages and the exact commands to build/run/test.
- State assumptions briefly. Ask at most one clarifying question, and only if blocked.
- Work in small steps that compile and run. Add or update tests with every change.
- When I paste console or runtime errors, diagnose the root cause and fix it; don't guess at unrelated changes.
- Explain non-obvious design decisions in 1-2 sentences, suitable for an ADR.
</working_rules>

<current_task>
Milestone M1: CMS integration and content snapshots. [Replace with M2..M7 as you progress.]
Scope: solution scaffold, PostgreSQL schema + migrations for tenants, cms_connections, content_items, content_versions, content_blocks, webhook_inbox; IContentSource with LocalTestContentSource and an OptimizelySaaSContentSource skeleton; POST /api/v1/content/sync; immutable snapshot storage; idempotent ingestion; tests.
</current_task>

<definition_of_done>
Builds clean, migrations apply, tests pass, sync endpoint works end to end against the test source, and a short note lists what to verify against the real Optimizely API.
</definition_of_done>
````

## 7. Caveats

- **Unverified external facts:** The feedback request, the `Optimizely.Opal.Tools` package and version (0.7.8), the CMS SaaS endpoints, and the Graph webhook behavior are all as claimed in your source. Check them against Optimizely's docs before building or putting them in a video.
- **Reconstructed state machine:** Only the node names survived in the pasted text, so the transition table above is my reconstruction.
- **Gaps I patched:**
  - The doc's `localization_jobs` table was missing `priority`, `locked_by`, `max_attempts`, and `last_error_code`.
  - The TM unique index omitted `content_type`.
  - The orchestrator interface lacked the `AnalyzeAsync` that the Opal example calls.
  - The doc uses `FAILED` in the state machine but `DEAD_LETTER` in the retry policy.
  - Redis is in the stack but not in the core scope, so treat it as optional.
 