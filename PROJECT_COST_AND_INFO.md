# OptiFlow — Implementation Token Cost & Project Information

> **Purpose:** Central reference for (1) estimated and tracked AI coding-agent token costs across Setup and Milestones M1–M7, (2) OptiFlow's runtime AI token-savings model, and (3) complete architectural, security, stack, and guardrail documentation derived from [Instruction.md](./Instruction.md).

---

## 1. Implementation Token Cost Summary (AI-Assisted Development)

> **Transparency Note:** Antigravity does not export exact IDE/CLI billing counters into workspace files automatically. Figures under **Estimated** are engineering projections based on milestone scope and multi-turn tool calls; **Observed Artifacts** reflect verified counts on disk; **Actual Tracked** columns are updated at the end of each milestone.

### Per-Milestone Token Budget & Tracker

| Milestone | Scope | Est. Input Tokens (incl. context/reads) | Est. Output Tokens (code/tests/docs) | Actual Input | Actual Output | Actual Cost (USD) | Observed Files | Status |
|---|---|---|---|---|---|---|---|---|
| **Setup** | `.agents/` rules, 4 hooks, 6 skills, 9-project `.NET 8` solution, Docker/OTel/Grafana, docs | 180k – 260k | 25k – 35k | — *(not exported)* | — *(not exported)* | — | 51 created | ✅ Complete |
| **M1** | CMS integration, domain model, PostgreSQL schema + migrations (`InitialSchema` + `AddImmutabilityAndRlsPolicies`), `IContentSource`, `POST /api/v1/content/sync`, idempotent ingestion, tests | 300k – 500k | 30k – 50k | — *(not exported)* | — *(not exported)* | — | Included below | ✅ Complete *(Offline)* |
| **M2** | Version-aware diff engine (`ContentDiffEngine`), block extraction, SHA-256 normalization, stable ID + field-path matching, diff API, tests | 200k – 350k | 20k – 35k | — *(not exported)* | — *(not exported)* | — | Included below | ✅ Complete |
| **M3** | Translation memory (`TranslationMemoryService`), composite lookup key, `HUMAN_APPROVED`-only gate, glossary terms, optional `RedisCacheService`, tests | 220k – 380k | 22k – 38k | — *(not exported)* | — *(not exported)* | — | Included below | ✅ Complete *(Redis fallback verified)* |
| **M4** | Local AI processing (`LmStudioLanguageModelClient`, Gemma 4B, concurrency 1), strict JSON, 7 deterministic validators, Worker `PostgresJobQueue` (`SKIP LOCKED`), retry/dead-letter, `ai_invocations` | 350k – 600k | 35k – 60k | — *(not exported)* | — *(not exported)* | — | Included below | ✅ Complete *(Mocked LLM)* |
| **M5** | Human-edit-safe merge engine (`MergeEngine`, 7 merge rules), conflict detection, flagship 100-block/3-changed integration test | 250k – 420k | 25k – 42k | — *(not exported)* | — *(not exported)* | — | Included below | ✅ Complete |
| **M6** | Review workflow API (`ReviewService`, ETag/`STALE_CONFLICT` 409), SignalR `LocalizationHub`, React + TypeScript 4-screen dashboard (`frontend/react-dashboard/`), tests | 450k – 800k | 50k – 85k | — *(not exported)* | — *(not exported)* | — | Included below | ✅ Complete |
| **M7** | OpenTelemetry traces/metrics (`OptiFlowTelemetry`), Grafana overview dashboard, analytics endpoints, 9 ADRs (`Accepted`), P1 Opal tool handlers (`OptiFlow.Opal`) | 300k – 500k | 30k – 50k | — *(not exported)* | — *(not exported)* | — | Included below | ✅ Complete *(Offline; live Docker/LM Studio pending)* |
| **Total (Setup + M1–M7)** | **Full Project Delivery** | **2.25M – 3.81M** | **237k – 395k** | **— *(session billing not exported)*** | **—** | **—** | **41 handwritten + 5 migration `.cs` (10,306 LOC), 9 test `.cs` (1,321 LOC, 31/31 offline tests passing), 11 frontend `.ts/.tsx/.css` (1,423 LOC), 9 ADRs** | **8 / 8 Phases Implemented (Offline Verified)** |

### Illustrative Model Cost Comparison (Full Project: Setup + M1–M7)

Because effective input cost depends heavily on prompt caching across multi-turn agent loops, the ranges below illustrate total USD cost assuming ~60–70% cached input reads and standard API rates:

| Model Tier | Illustrative Rate (Input / Cached / Output per 1M) | Est. Full Project Cost (Setup + M1–M7) | Best Fit |
|---|---|---|---|
| **Fast / Light** (e.g., Flash / Mini class) | ~$0.15–$0.50 / ~$0.04–$0.12 / ~$0.60–$2.00 | **~$0.40 – $2.10** | Boilerplate, DTOs, unit test scaffolding, docs |
| **Balanced / Standard** (e.g., Pro / Sonnet class) | ~$1.25–$3.00 / ~$0.30–$0.75 / ~$5.00–$15.00 | **~$2.80 – $12.80** | Core backend services, EF Core, React UI |
| **Frontier / Heavy Reasoning** | ~$5.00–$15.00 / ~$1.25–$3.75 / ~$15.00–$75.00 | **~$9.50 – $48.00** | Architecture design, merge/concurrency invariants, code review |

> **Cost-Optimization Practices Configured in This Workspace:**
> 1. `architecture-reminder` hook runs on `SessionStart` (once per session), NOT on every `PreInvocation`, preventing repeated prompt inflation.
> 2. Skills (`.agents/skills/*/SKILL.md`) use progressive disclosure — loaded only when relevant to the active task.
> 3. Work one milestone per fresh session (or use `/compact` between milestones) to keep context windows lean.

---

## 2. OptiFlow Product Runtime AI Token Economics

OptiFlow's core business purpose (from `Instruction.md`) is reducing wasted localization tokens and protecting human edits:

| Aspect | Specification |
|---|---|
| **Local LLM Target** | Gemma 4B quantized via **LM Studio** (OpenAI-compatible API) on RTX 3060 Ti (8 GB VRAM) / 32 GB RAM |
| **Concurrency Default** | `1` (`SemaphoreSlim(1, 1)`) until p95 latency and VRAM benchmarks justify scaling |
| **Direct API Token Fee (Local)** | `$0.00` per token (operating cost = local electricity + hardware time) |
| **Cloud Comparison Metric** | `ai_invocations` records `input_tokens`, `output_tokens`, `latency_ms`, and `estimated_cost` per block to compare incremental vs. full-page cloud baseline |
| **Flagship Savings Test** | **100 blocks, 3 changed** → **0 AI calls** for 97 unchanged blocks (`97%` block AI call reduction), `3` blocks processed |
| **Projection Caveat** | The **80–90% AI credit reduction** figure referenced in `Instruction.md` is an unverified customer projection until M7 benchmarks run |

---

## 3. Complete Project Technical Reference

### 3.1 Tech Stack & Verified NuGet Versions (`OptiFlow/Directory.Packages.props`)

| Layer | Technology / Package | Version |
|---|---|---|
| **Runtime / Language** | .NET 8 (`net8.0`), C# 12, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` | SDK `net8.0` |
| **Web API & Docs** | `Microsoft.AspNetCore.OpenApi`, `Swashbuckle.AspNetCore` | `8.0.11`, `6.9.0` |
| **Database & ORM** | PostgreSQL 16 (`postgres:16-alpine`), `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` | `8.0.11` |
| **Cache (Optional P1)** | Redis 7 (`redis:7-alpine`), `Microsoft.Extensions.Caching.StackExchangeRedis` | `8.0.11` |
| **Observability** | `OpenTelemetry.Exporter.OpenTelemetryProtocol` / `Extensions.Hosting` (`1.15.3`), `Instrumentation.AspNetCore` (`1.15.2`), `Instrumentation.EntityFrameworkCore` (`1.0.0-beta.12`), `Serilog.AspNetCore` (`8.0.3`), OTel Collector (`0.109.0`), Grafana (`11.3.0`) | Verified (`0 warnings`) |
| **Validation** | `FluentValidation.AspNetCore` | `11.3.0` |
| **Testing** | `xunit` (`2.9.2`), `Moq` (`4.20.72`), `FluentAssertions` (`6.12.2`), `Testcontainers.PostgreSql` (`3.10.0`), `Microsoft.AspNetCore.Mvc.Testing` (`8.0.11`), `NetArchTest.Rules` (`1.3.2`) | Verified |
| **Frontend** | React 18 + TypeScript + Vite + TanStack Query + SignalR (`OptiFlow/frontend/react-dashboard/`) | Built (`tsc --noEmit` + `vite build` pass) |
| **CMS & Opal** | Optimizely CMS SaaS (`IContentSource` skeleton); `Optimizely.Opal.Tools` (`0.7.8` confirmed on NuGet; `OptiFlow.Opal` tool handlers implemented calling Application services) | Verified NuGet `0.7.8`; live Optimizely tenant deferred |

### 3.2 Solution Architecture & Dependency Graph

```text
OptiFlow.Domain          -> (zero external or project dependencies)
OptiFlow.Application     -> OptiFlow.Domain
OptiFlow.Infrastructure  -> OptiFlow.Application, OptiFlow.Domain
OptiFlow.Api             -> OptiFlow.Application, OptiFlow.Infrastructure
OptiFlow.Worker          -> OptiFlow.Application, OptiFlow.Infrastructure
OptiFlow.Opal            -> OptiFlow.Application
```

Tests: `OptiFlow.UnitTests`, `OptiFlow.IntegrationTests` (Testcontainers PostgreSQL), `OptiFlow.ArchitectureTests` (`NetArchTest.Rules`).

### 3.3 Core Pipeline, State Machine & Merge Rules

- **9-Step Pipeline:** `Ingest (webhook_inbox) → Immutable Snapshot → Block Diff (stable ID / field path + SHA-256) → Translation Memory (HUMAN_APPROVED only) → Local AI (Gemma strict JSON) → Deterministic Validation (+ optional semantic) → Merge (preserve human edits) → Human Review → CMS Draft (ETag check, never auto-publish)`.
- **14 Job States:** `QUEUED`, `SNAPSHOTTING`, `DIFFING`, `TRANSLATING`, `VALIDATING`, `RETRY_WAIT`, `NEEDS_MANUAL_REVIEW`, `AWAITING_REVIEW`, `APPROVED`, `REJECTED`, `CMS_DRAFT_CREATED`, `STALE_CONFLICT`, `FAILED`, `DEAD_LETTER`.
- **Retry Policy:** `0s → 2s → 4s → 8s → DEAD_LETTER` for transient failures only; never retry validation or permission errors.
- **7 Merge Rules:**
  1. Source unchanged → keep existing translation (0 AI calls)
  2. Modified + approved TM hit → reuse TM entry
  3. Modified + machine-managed target → translate & replace
  4. Source unchanged + human-edited target → preserve
  5. Modified + human-edited target → **CONFLICT → send to reviewer**
  6. Added block → TM lookup, else translate
  7. Deleted block → propose removal in draft

### 3.4 PostgreSQL Schema (14 Tables & Unique Keys)

Tables: `tenants`, `cms_connections`, `content_items`, `content_versions` (immutable), `content_blocks`, `localization_jobs` (`FOR UPDATE SKIP LOCKED` + lease), `localization_job_blocks`, `translation_memory_entries`, `glossary_terms`, `review_tasks`, `ai_invocations`, `audit_events`, `webhook_inbox`, `outbox_messages`.

Required `UNIQUE` constraints:
- `content_items`: `(tenant_id, connection_id, external_content_key)`
- `content_versions`: `(content_item_id, external_version_key, locale)`
- `content_blocks`: `(content_version_id, block_key)`
- `translation_memory_entries`: `(tenant_id, source_locale, target_locale, source_hash, glossary_version, content_type)`
- `webhook_inbox`: `(tenant_id, provider, external_event_id)`

### 3.5 Nine Non-Negotiable Invariants

1. Content snapshots are immutable — never update an old version row.
2. Never auto-publish — only create CMS drafts after explicit human approval.
3. Never overwrite a human-edited translation without reviewer conflict resolution.
4. Translation memory reuses `HUMAN_APPROVED` entries only; AI output is never auto-approved.
5. Duplicate CMS events never create duplicate jobs (idempotent via `webhook_inbox`).
6. Check latest CMS version/ETag before creating a draft; mismatch → `STALE_CONFLICT`.
7. Validate every LLM output deterministically (JSON schema, non-empty, HTML tags, placeholders, glossary, length guard, source currency); never trust model `confidence`.
8. Logs contain IDs, hashes, and redacted metadata — never raw customer content.
9. Fuzzy/semantic matching is a suggestion only — never an automatic overwrite.

---

## 4. Configured Guardrails, Hooks & Skills

### Workspace Rules (`.agents/AGENTS.md`)
Always-on rules covering Clean Architecture dependency direction, `.NET 8` coding standards, React + TypeScript rules, AI/Observability rules, security & tenant isolation, PostgreSQL conventions, testing standards, error handling, state machine rules, and tracker updates.

### Lifecycle Hooks (`.agents/hooks.json`)

| Hook | Trigger | Script | Behavior |
|---|---|---|---|
| `secret-scanner` | `PreToolUse` (`write_file\|edit_file`) | `.agents/hooks/secret-scan.cmd` → `.ps1` | Blocks writes to `.env`, `secrets.json`, `appsettings.*.local.json`, `*.pem/*.key`; blocks hardcoded passwords, `sk-*` keys, JWTs, private keys, AWS/GitHub/Optimizely secrets; blocks `OptiFlow.Domain.csproj` package/project references |
| `command-guard` | `PreToolUse` (`run_command`) | `.agents/hooks/command-guard.cmd` → `.ps1` | Blocks staging secret files (`git add .env`); prompts (`ask`) before `ef database drop`, `DROP TABLE`, `git push --force`, `git reset --hard` |
| `dotnet-build-check` | `PostToolUse` (`write_file\|edit_file`) | `.agents/hooks/build-check.cmd` → `.ps1` | On `.cs`/`.csproj`/`.props`/`.sln` edits, runs `dotnet build --no-restore --verbosity quiet` and logs to `.agents/hooks/last-build.log` |
| `architecture-reminder` | `SessionStart` | `.agents/hooks/arch-reminder.cmd` | Injects the 9 architectural invariants once at session start |

### Stack Skills (`.agents/skills/`)

| Skill | Focus |
|---|---|
| `dotnet-patterns` | ASP.NET Core wiring, DI, EF Core configurations, value object conversions, Worker Service, `Result<T>`, controllers, OTel |
| `postgresql-patterns` | `FOR UPDATE SKIP LOCKED`, immutable snapshot triggers, JSONB conventions, composite unique keys, RLS, webhook deduplication |
| `optiflow-architecture` | 9-step pipeline, domain model, core interfaces, 14-state machine, 7 merge rules, 9 invariants, AI JSON format, REST API, schema |
| `security-checklist` | Tenant isolation, User Secrets / vault handling, input validation, log redaction, draft-only CMS rule, TM approval rule, AI safety |
| `react-typescript-patterns` | 4 dashboard screens, strict TS, TanStack Query, SignalR real-time hook, XSS-safe translation diffing, `STALE_CONFLICT` UX |
| `ai-observability-patterns` | LM Studio / Gemma 4B local strategy, concurrency=1, 7 deterministic validators, Redis optional cache-aside, OTel tracing & redaction |

---

## 5. Quickstart & Verification Commands

```powershell
# Start PostgreSQL, Redis, OTel Collector, and Grafana
cd f:\coding\_LinkdingProject\Episod2_First\OptiFlow
docker compose up -d

# Configure local secrets safely (never commit secrets)
cd src\OptiFlow.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=optiflow;Username=optiflow;Password=optiflow_dev"

# Restore, build, and test solution
cd f:\coding\_LinkdingProject\Episod2_First\OptiFlow
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

## 6. External Caveats to Verify (from `Instruction.md`)

Before live integration in M1 / M6:
1. Verify Optimizely CMS SaaS Content Management API endpoints, auth flow, and ETag headers against official Optimizely documentation.
2. Confirm `Optimizely.Opal.Tools` package availability and version (`0.7.8` claimed in `Instruction.md`) on NuGet before P1 work.
3. Confirm Graph webhook event schema/behavior (bulk-completed and expiry notifications only).
4. Recommended before starting M1: run `git init` at workspace root (`f:\coding\_LinkdingProject\Episod2_First`) so every milestone commits cleanly.
