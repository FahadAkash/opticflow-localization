# OptiFlow — Project Tracker

> Synchronized with root [PROJECT_COST_AND_INFO.md](../PROJECT_COST_AND_INFO.md).

## Project Overview

| Field | Value |
|---|---|
| Project | OptiFlow — Incremental Localization Engine for Optimizely CMS |
| Backend | ASP.NET Core (.NET 8), Worker Service |
| Database | PostgreSQL 16 (job queue via `FOR UPDATE SKIP LOCKED`) |
| Cache | Redis 7 (optional — P1) |
| AI | Gemma 4B quantized via LM Studio (OpenAI-compatible API) |
| Frontend | React + TypeScript |
| Observability | OpenTelemetry + Grafana |
| CMS Target | Optimizely CMS SaaS Content Management API |

---

## Token Cost Summary

| Milestone | Phase | Est. Input Tokens | Est. Output Tokens | Actual Input / Output | Actual Cost (USD) | Files Created/Modified | Tests Written | Status |
|---|---|---|---|---|---|---|---|---|
| Setup | Scaffolding, Guardrails, Hooks & Skills | 180k – 260k | 25k – 35k | — *(not exported)* | — | 51 created | 0 (skeleton) | ✅ Complete |
| M1 | CMS Integration + Snapshots | 300k – 500k | 30k – 50k | — | — | Domain/App/Infra/Migrations | 6 Arch + Sync/Webhook tests | ✅ Complete *(Offline)* |
| M2 | Version-Aware Diff Engine | 200k – 350k | 20k – 35k | — | — | `ContentDiffEngine` + API | 4 Diff unit tests | ✅ Complete |
| M3 | Translation Memory | 220k – 380k | 22k – 38k | — | — | `TranslationMemoryService` + `RedisCacheService` | 3 TM unit tests | ✅ Complete |
| M4 | Local AI Processing | 350k – 600k | 35k – 60k | — | — | `LmStudioLanguageModelClient` + 7 Validators + `PostgresJobQueue` + Worker | 9 Validator/Queue/LM tests | ✅ Complete *(Mocked LLM)* |
| M5 | Human-Edit-Safe Merge | 250k – 420k | 25k – 42k | — | — | `MergeEngine` + Orchestrator | 3 Merge/State tests + Flagship 100-block E2E | ✅ Complete |
| M6 | Review + CMS Draft + Dashboard | 450k – 800k | 50k – 85k | — | — | `ReviewService` + SignalR + 4-screen React dashboard | Review/`STALE_CONFLICT` E2E + TS/Vite build | ✅ Complete |
| M7 | Benchmarks + Observability + Opal | 300k – 500k | 30k – 50k | — | — | OTel + Grafana + Analytics + `OptiFlow.Opal` + 9 ADRs | 2 Opal/Analytics unit tests | ✅ Complete *(Offline)* |

### Cumulative Totals

| Metric | Value |
|---|---|
| Est. Total Input Tokens (Setup + M1–M7) | 2.25M – 3.81M |
| Est. Total Output Tokens (Setup + M1–M7) | 237k – 395k |
| Tracked Actual Input / Output Tokens | — *(session export not enabled)* |
| Tracked Actual Cost | — |
| Production C# Files / Lines (Observed) | 41 handwritten `.cs` (`6,799` LOC) + 5 migration `.cs` (`3,507` LOC) = `46` files (`10,306` LOC) |
| Frontend Source Files / Lines (Observed) | 11 `.ts/.tsx/.css` files (`1,423` LOC) |
| Tests Passing (Observed Offline) | **31 / 31 passing** (`6` Architecture, `19` Unit, `6` offline Integration) + `1` Docker-tagged Testcontainers test (`[Trait("Category", "Docker")]`) |

---

## Milestone Definitions

### M1: CMS Integration + Content Snapshots
- PostgreSQL schema + migrations for: `tenants`, `cms_connections`, `content_items`, `content_versions`, `content_blocks`, `webhook_inbox`
- `IContentSource` with `LocalTestContentSource` and `OptimizelySaaSContentSource` skeleton
- `POST /api/v1/content/sync` endpoint
- Immutable snapshot storage (including DB update-prevention trigger)
- Idempotent webhook ingestion
- Tests: architecture boundary tests, snapshot immutability, idempotency, sync endpoint E2E

### M2: Version-Aware Diff Engine
- `IContentDiffEngine` implementation
- Block extraction with stable `block_key`
- SHA-256 normalization and hashing
- Block matching (CMS ID first, field path fallback)
- Diff result: `unchanged`, `modified`, `added`, `deleted`
- Tests: diff accuracy, reordering tolerance, hash consistency

### M3: Translation Memory
- `ITranslationMemory` implementation
- Lookup key: tenant + source locale + target locale + source hash + glossary version + content type
- Approved-only reuse policy (`HUMAN_APPROVED`)
- Provenance tracking: `AI_GENERATED`, `HUMAN_EDITED`, `HUMAN_APPROVED`
- Tests: lookup accuracy, approval gating, provenance integrity

### M4: Local AI Processing
- `ILanguageModelClient` with LM Studio adapter (Gemma 4B, concurrency 1)
- Structured JSON prompt/response
- 7 deterministic validators
- Timeout, cancellation, bounded concurrency, `SKIP LOCKED` job queue + lease recovery
- Token tracking per invocation (`ai_invocations`)
- Tests: JSON validation, retry behavior (`0s/2s/4s/8s -> DEAD_LETTER`), timeout handling

### M5: Human-Edit-Safe Merge
- Merge engine implementing all 7 rules
- Human-edit detection and preservation
- Conflict flagging for reviewer
- Tests: flagship 100-block/3-changed test, human-edit preservation, conflict detection

### M6: Review + CMS Draft
- Review task workflow (`AWAITING_REVIEW` → `APPROVED`/`REJECTED`)
- Side-by-side 4-screen review dashboard (`frontend/react-dashboard/`)
- CMS draft creation with ETag/version check (`STALE_CONFLICT` handling)
- Tests: approval flow, rejection flow, version mismatch

### M7: Benchmarks + Observability
- OpenTelemetry tracing across API, Worker, and AI
- Grafana dashboards
- Performance benchmarks (p50/p95/p99)
- Cost analytics (AI credits vs. baseline, reuse rate)
- Load testing
- Optional P1 Opal tools (`OptiFlow.Opal`)
- 9 ADRs documented in `OptiFlow/docs/decisions/`

---

## Architecture Decisions Log

| ADR # | Decision | Rationale | Date | Status |
|---|---|---|---|---|
| ADR-001 | Modular monolith over microservices | Production-style separation without 10 microservices. API + Worker as separate processes. | 2026-11-11 | Accepted |
| ADR-002 | PostgreSQL job queue (`SKIP LOCKED`) | Queue and business state commit in one transaction. No external broker dependency. | 2026-11-11 | Accepted |
| ADR-003 | Immutable content snapshots | New version = new row. Never modify history. Enables reliable diffing. | 2026-11-11 | Accepted |
| ADR-004 | Stable block identity + SHA-256 diff | Match by CMS block ID (fallback: field path). Hash normalized text for change detection. | 2026-11-11 | Accepted |
| ADR-005 | Translation memory (approved-only) | Only `HUMAN_APPROVED` entries reused. AI output never auto-approved. | 2026-11-11 | Accepted |
| ADR-006 | Local LLM via adapter pattern | Gemma via LM Studio behind `ILanguageModelClient`. Swappable for cloud LLM later. | 2026-11-11 | Accepted |
| ADR-007 | Human approval before CMS draft | No auto-publish. Reviewer must approve. Draft created with optimistic concurrency. | 2026-11-11 | Accepted |
| ADR-008 | Optimistic concurrency (ETag/version) | Check CMS version before draft creation. Mismatch → `STALE_CONFLICT` → re-diff. | 2026-11-11 | Accepted |
| ADR-009 | OpenTelemetry end-to-end tracing | Trace IDs across API, Worker, and AI. Exported to Grafana via OTLP. | 2026-11-11 | Accepted |

---

## Non-Functional Targets

| Category | Target | Metric | Verified |
|---|---|---|---|
| API Latency | p95 < 300ms (excl. external calls) | Response time | ⏳ Pending live PostgreSQL benchmark |
| Availability | 99.5% design target | Uptime | ⏳ Production deployment |
| Idempotency | Zero duplicate jobs | `UNIQUE(tenant_id, provider, external_event_id)` | ✅ Offline integration test |
| Data Integrity | 100% unchanged block preservation | Automated test | ✅ Flagship 100-block test |
| Concurrency | Bounded worker concurrency, stateless workers | `SemaphoreSlim(1,1)` + worker scopes | ✅ Unit/Integration verified |
| Security | Encrypted credentials at rest | DataProtection vault refs (`dp-v1:`) + tenant query filters | ✅ Integration verified (PostgreSQL RLS script generated) |
| Recoverability | Expired leases reclaimed after crash | Lease timeout test | ✅ Offline integration test |
| Consistency | Optimistic concurrency on CMS writes | ETag test | ✅ `409 STALE_CONFLICT` test |
| AI Reliability | Invalid output rejected and retried | Validation test | ✅ Deterministic validator + `DEAD_LETTER` tests |
| Observability | End-to-end trace IDs | `ActivitySource` + `Meter` | ⏳ Live OTel Collector pending Docker |

---

## Risk Register

| Risk | Impact | Mitigation | Status |
|---|---|---|---|
| LM Studio performance on RTX 3060 Ti 8GB | AI latency exceeds acceptable thresholds | Start with 4B quantized model, concurrency 1, benchmark before scaling | Open |
| Optimizely CMS SaaS API undocumented behavior | Integration failures | `LocalTestContentSource` for dev, validate against real API in live sandbox | Open |
| `Optimizely.Opal.Tools` package version (0.7.8) availability | Build failure | Confirmed `Optimizely.Opal.Tools` `0.7.8` exists on NuGet; `OptiFlow.Opal` tool handlers tested | Mitigated |
| Graph webhook not a per-save webhook | Missing real-time updates | Support 3 ingestion methods (manual, integration event, Graph) | Open |
| Redis dependency in critical path | Cache failure blocks processing | Redis is optional, `RedisCacheService` falls back gracefully to PostgreSQL | Mitigated |
