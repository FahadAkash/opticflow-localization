# optiflow-localization

Incremental block-level localization engine for **Optimizely CMS** built with **.NET 8**, **PostgreSQL**, **React + TypeScript**, **OpenTelemetry**, and local LLM inference (**Gemma** via **LM Studio**).

## Problem & Approach

Small edits to master CMS content often trigger full-page retranslation, wasting AI credits and risking manual translation edits. **OptiFlow** sits beside the CMS (never replacing it or auto-publishing) and executes a 9-step incremental pipeline:

1. **Ingest** — CMS event or manual sync deduplicated via `webhook_inbox`.
2. **Snapshot** — Immutable `content_versions` and `content_blocks` storage.
3. **Diff** — Match blocks by stable CMS ID (falling back to field path) and compare SHA-256 hashes of normalized text.
4. **Translation Memory** — Reuse only `HUMAN_APPROVED` matches.
5. **Local AI** — Translate remaining blocks via `ILanguageModelClient` (LM Studio / Gemma) returning strict JSON.
6. **Validate** — Run mandatory deterministic checks (JSON schema, non-empty, HTML tags, placeholders, glossary, length guard, source currency).
7. **Merge** — Preserve human-edited blocks; route modified human-edited conflicts to reviewers.
8. **Review** — Side-by-side review dashboard (`AWAITING_REVIEW` → `APPROVED` / `REJECTED`).
9. **Draft** — Create a CMS **draft** with optimistic concurrency (`ETag`/version check → `STALE_CONFLICT` on mismatch). Never auto-publishes.

## Repository Structure

```text
.
├── .agents/                    # Workspace rules (AGENTS.md), lifecycle hooks, stack skills, tracker
├── OptiFlow/
│   ├── src/
│   │   ├── OptiFlow.Domain/          # Entities, value objects, domain interfaces (zero external refs)
│   │   ├── OptiFlow.Application/     # Orchestration, diff/TM/merge use cases
│   │   ├── OptiFlow.Infrastructure/  # EF Core (PostgreSQL), CMS adapters, AI gateway, Redis, logging
│   │   ├── OptiFlow.Api/             # ASP.NET Core REST API & webhooks
│   │   ├── OptiFlow.Worker/          # Background job worker (FOR UPDATE SKIP LOCKED)
│   │   └── OptiFlow.Opal/            # Optimizely Opal tools (P1)
│   ├── frontend/react-dashboard/     # React + TypeScript ops & review dashboard (M6)
│   ├── tests/                        # UnitTests, IntegrationTests (Testcontainers), ArchitectureTests
│   ├── infrastructure/               # Dockerfiles, OpenTelemetry Collector, Grafana provisioning
│   ├── docs/                         # Architecture, ADRs, API docs, benchmarks
│   ├── OptiFlow.sln
│   └── docker-compose.yml
├── Instruction.md              # Architecture & HLD/LLD specification
└── PROJECT_COST_AND_INFO.md    # Token cost tracker, runtime economics & technical reference
```

## Quickstart

```powershell
# 1. Start PostgreSQL, Redis, OTel Collector, and Grafana
cd OptiFlow
docker compose up -d

# 2. Restore, build, and test
dotnet restore --nologo -v q
dotnet build --no-restore --nologo -v q
dotnet test --no-build --nologo --verbosity minimal
```

## Documentation

- [Instruction.md](./Instruction.md) — Full HLD, LLD, and architecture prompt.
- [PROJECT_COST_AND_INFO.md](./PROJECT_COST_AND_INFO.md) — Implementation token cost plan, runtime savings model, schema/API reference, and milestones (M1–M7).
- [.agents/AGENTS.md](./.agents/AGENTS.md) — Coding standards, Clean Architecture boundaries, and security guardrails.
