# OptiFlow — Workspace Rules

## Architecture Rules

- Follow Clean Architecture: Domain ← Application ← Infrastructure.
- Domain project (`OptiFlow.Domain`) MUST NOT reference any infrastructure or
  NuGet packages (no EF Core, no Npgsql, no HTTP clients, no System.Text.Json
  attributes). Domain defines interfaces; Infrastructure implements them.
- All CMS-specific code MUST live behind `IContentSource`. Never scatter
  Optimizely API calls across services.
- API controllers are thin: validate input → call Application service → map
  result to HTTP response. No business logic in controllers.
- Application services orchestrate domain logic. They depend on domain
  interfaces, never on concrete infrastructure classes.
- Worker (`OptiFlow.Worker`) depends on Application, never directly on
  Infrastructure internals.
- Dependency direction is strictly enforced:
  - `Api` → `Application`, `Infrastructure`
  - `Worker` → `Application`, `Infrastructure`
  - `Opal` → `Application`
  - `Application` → `Domain`
  - `Infrastructure` → `Application`, `Domain`
  - `Domain` → (nothing)

## .NET Coding Standards

- Target .NET 8 (`net8.0`). Use C# 12 features (primary constructors,
  collection expressions, raw string literals) where they improve clarity.
- Use `record` types for DTOs and value objects. Use `sealed` on classes that
  are not designed for inheritance.
- Every public API method MUST accept `CancellationToken` as the last parameter.
- Use `ILogger<T>` from `Microsoft.Extensions.Logging`. Never use
  `Console.WriteLine` in production code.
- Use `TimeProvider` (not `DateTime.UtcNow` or `DateTime.Now`) for testable
  time dependencies.
- Nullable reference types MUST be enabled in all projects
  (`<Nullable>enable</Nullable>`).
- Treat warnings as errors in CI builds
  (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
- Use file-scoped namespaces (`namespace X;`) consistently.
- Prefer `ValueTask<T>` over `Task<T>` only when the hot path is synchronous
  completion. Default to `Task<T>`.
- Mark async methods with the `Async` suffix (e.g., `GetContentAsync`).
- Use `ConfigureAwait(false)` in library projects (Application, Domain,
  Infrastructure). Omit it in Api and Worker (ASP.NET Core SynchronizationContext
  handles it).

## Frontend (React + TypeScript) Rules

- Live in `OptiFlow/frontend/react-dashboard/` with TypeScript `strict: true`.
- Never use `any`; define typed interfaces matching API JSON contracts.
- Never render raw CMS/AI HTML with `dangerouslySetInnerHTML` without strict
  sanitization. Prefer plain-text/token diff rendering by default.
- Never store secrets or CMS keys in `VITE_*` environment variables.
- Handle `409 Conflict` (`STALE_CONFLICT`) explicitly in Review UI.

## AI, Caching & Observability Rules

- Local AI calls go behind `ILanguageModelClient` (Gemma 4B quantized via LM
  Studio, default concurrency 1 until benchmarked, strict JSON + timeouts).
- Run all deterministic checks (JSON schema, non-empty, HTML tags,
  placeholders, glossary, length guard, source version current) on every AI
  output. Never trust model `confidence` as an approval gate.
- Redis caching is optional: always prefix cache keys with `tenant:{tenantId}:`
  and fall back gracefully to PostgreSQL if Redis is disabled or unavailable.
- Emit OpenTelemetry traces (`OptiFlow.*`) and metrics with `tenant_id`,
  `job_id`, hashes, and token counts only.

## Security Rules

- NEVER log raw customer content (source text, translations, prompts, or AI
  outputs). Logs contain only IDs, hashes, and redacted metadata.
- NEVER store secrets (API keys, connection strings with passwords, JWT signing
  keys) in source code, `appsettings.json`, or environment files committed to
  VCS. Use User Secrets locally (`dotnet user-secrets`) and a vault in
  production.
- Every tenant-scoped query MUST include `WHERE tenant_id = @tenantId`. No
  cross-tenant data leakage. Enforce via Row-Level Security where appropriate.
- Credentials stored in `cms_connections.credential_ref` are references to an
  encrypted vault, never plaintext values.
- NEVER auto-publish content to the CMS. Only create drafts after explicit
  human reviewer approval.
- NEVER auto-approve AI-generated translations into translation memory. Only
  entries with provenance `HUMAN_APPROVED` are reusable.
- Validate ALL external input at the boundary:
  - Webhook payloads: verify signature, check payload hash, deduplicate via
    `webhook_inbox`.
  - API requests: use FluentValidation or DataAnnotations.
  - CMS responses: null-check, schema-validate before processing.
- Use parameterized queries / EF Core parameters exclusively. Never concatenate
  SQL strings. Never use `FromSqlRaw` with string interpolation.
- Sanitize all user-facing error messages. Never expose stack traces, connection
  strings, or internal paths in API responses.

## Database Rules

- Run EF Core migrations from `OptiFlow/`:
  `dotnet ef migrations add <Name> --project src/OptiFlow.Infrastructure --startup-project src/OptiFlow.Api`.
- Snapshots (`content_versions`) are immutable — never UPDATE an existing version
  row. New version = new row.
- JSONB columns are ONLY for: `raw_payload`, `validation_result`, `metadata`
  (audit). Queryable fields stay as strongly typed columns.
- Every new table with tenant data MUST have a `tenant_id` column with a
  composite index.
- Use `FOR UPDATE SKIP LOCKED` for job claiming. Never use optimistic retry
  loops for queue contention.
- Use `UNIQUE` constraints as specified in the schema:
  - `content_items`: `UNIQUE(tenant_id, connection_id, external_content_key)`
  - `content_versions`: `UNIQUE(content_item_id, external_version_key, locale)`
  - `content_blocks`: `UNIQUE(content_version_id, block_key)`
  - `translation_memory_entries`: `UNIQUE(tenant_id, source_locale, target_locale, source_hash, glossary_version, content_type)`
  - `webhook_inbox`: `UNIQUE(tenant_id, provider, external_event_id)`

## Testing Rules

- Every new feature or bug fix MUST include at least one test.
- Unit tests go in `tests/OptiFlow.UnitTests/`.
- Integration tests go in `tests/OptiFlow.IntegrationTests/`.
- Architecture tests go in `tests/OptiFlow.ArchitectureTests/`.
- Name tests using the pattern: `MethodName_Scenario_ExpectedResult`.
- Use `Verify` or inline snapshots for complex assertion outputs.
- The flagship integration test: 100 blocks with 3 changed → zero AI calls for
  97 unchanged, 3 processed, human edits preserved, no draft if validation fails.
- Use Testcontainers for PostgreSQL in integration tests.
- Mock external dependencies (CMS API, LM Studio) at the interface boundary.

## Error Handling

- Use `Result<T>` pattern for domain operations that can fail predictably.
  Throw exceptions only for truly exceptional / programmer errors.
- Retry policy: exponential backoff at 0s, 2s, 4s, 8s, then `DEAD_LETTER`.
- NEVER retry validation errors or permission errors — they fail permanently.
- Transient errors (network timeouts, temporary DB unavailability) are retried.
- Dead-lettered jobs require manual intervention or explicit re-queue.

## State Machine Rules

- Valid states: `QUEUED`, `SNAPSHOTTING`, `DIFFING`, `TRANSLATING`, `VALIDATING`,
  `RETRY_WAIT`, `NEEDS_MANUAL_REVIEW`, `AWAITING_REVIEW`, `APPROVED`, `REJECTED`,
  `CMS_DRAFT_CREATED`, `STALE_CONFLICT`, `FAILED`, `DEAD_LETTER`.
- `FAILED` is NOT automatically retryable.
- `REJECTED` can NEVER be published.
- `CMS_DRAFT_CREATED` means a draft exists in the CMS — NOT that it is published.
- `STALE_CONFLICT` triggers re-fetch of the latest CMS version and restart from
  `DIFFING`.

## Milestone & Cost Tracking Rules

- After completing each milestone (M1–M7) or substantial feature, update
  `PROJECT_COST_AND_INFO.md` and `.agents/PROJECT_TRACKER.md` with verified
  file/test/LOC counts, milestone status, ADRs, and updated token/cost logs.

## Git Commit Conventions

- Use conventional commits: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`,
  `chore:`, `ci:`.
- Each commit should compile and pass tests.
- Reference milestone in commit messages when applicable (e.g., `feat(M1): ...`).
