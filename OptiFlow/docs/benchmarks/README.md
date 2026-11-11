# OptiFlow Benchmarks & Verification Status

## Offline Verification Results (Verified Without Docker / Live LM Studio)

| Check | Result | Scope & Conditions |
|---|---|---|
| `dotnet build --no-restore --nologo -v q` | ✅ 0 Warnings, 0 Errors | All 9 `.NET 8` projects |
| `OptiFlow.ArchitectureTests` | ✅ 6 / 6 Passed | Clean Architecture layer isolation (`Domain`, `Application`, `Opal`, `Controllers`, `Worker`) |
| `OptiFlow.UnitTests` | ✅ 19 / 19 Passed | DiffEngine, TM approval gate, 7 deterministic validators, 7 merge rules, retry schedule, LM Studio mock handler, Opal & Analytics |
| `OptiFlow.IntegrationTests` (`Category!=Docker`) | ✅ 6 / 6 Passed | SQLite relational DB + `WebApplicationFactory` + `LocalTestContentSource` + `SpyLanguageModelClient` |
| Flagship 100-Block / 3-Changed Test | ✅ Passed | `0` AI calls for 97 unchanged blocks, `3` blocks processed, human-edited block preserved, `0` drafts created on validation failure |
| Frontend `tsc --noEmit` & `vite build` | ✅ Passed | `strict: true`, zero type errors, production bundle emitted (`dist/`) |
| EF Core Migration SQL Script (`init-schema.sql`) | ✅ Generated | Idempotent PostgreSQL DDL + `prevent_version_update()` trigger + 14 RLS policies |

## Pending Live Environment Benchmarks (Require Docker & LM Studio)

Run once Docker Desktop and LM Studio (4B quantized Gemma) are running:

```powershell
# 1. Start PostgreSQL, Redis, OTel Collector, Grafana
cd OptiFlow
docker compose up -d

# 2. Run live Testcontainers PostgreSQL integration test (SKIP LOCKED + trigger)
dotnet test --filter "Category=Docker"
```

- **API p95 latency (< 300ms target)**: Measure against live PostgreSQL + Redis.
- **LM Studio Gemma 4B latency & VRAM (RTX 3060 Ti 8GB)**: Benchmark `MaxConcurrency = 1` before raising concurrency.
