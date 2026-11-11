---
name: ai-observability-patterns
description: >-
  Use this skill when implementing the LM Studio / Gemma AI gateway,
  deterministic and semantic translation validators, optional Redis caching,
  or OpenTelemetry + Grafana + Serilog observability in OptiFlow.
---

# AI Gateway, Caching & Observability Patterns for OptiFlow

## 1. Local AI Gateway (`ILanguageModelClient` + LM Studio)

Target local hardware baseline (from `Instruction.md`): RTX 3060 Ti 8 GB, 32 GB RAM.

- **Model strategy**: Start with a **4B quantized Gemma** model hosted in LM Studio via its OpenAI-compatible HTTP endpoint (`/v1/chat/completions`).
- **Bounded concurrency**: Default to concurrency **1** (`SemaphoreSlim(1, 1)`) for local GPU inference; only raise after benchmarking p95 latency and VRAM headroom.
- **Timeouts & Cancellation**: Configure `HttpClient.Timeout` (e.g., 60s per block) and always pass `CancellationToken` down to `SendAsync`.
- **Warm-up**: Support a lightweight startup/health warm-up call so the first job does not fail on cold model load.
- **Strict JSON output**: Request structured JSON `{ "translated_text": "...", "confidence": 0.0 }` and parse strictly with `System.Text.Json`. Never treat `confidence` as a calibrated quality score or auto-approval gate.
- **Token & cost recording**: Always persist an `ai_invocations` record (`provider`, `model_name`, `prompt_version`, `input_tokens`, `output_tokens`, `latency_ms`, `estimated_cost`, `status`) even when validation fails.

### Retry vs. Permanent Failure

- **Transient errors** (HTTP 429/502/503, network timeout, malformed JSON retry up to bounded limit): transition to `RETRY_WAIT` with backoff `0s → 2s → 4s → 8s → DEAD_LETTER`.
- **Non-retryable errors** (HTTP 401/403 permission error, schema/domain invariant violation): fail permanently or route to `NEEDS_MANUAL_REVIEW`.

## 2. Deterministic Validation Pipeline (Mandatory)

Run deterministic checks in order BEFORE any optional semantic check:

1. **JSON Schema**: Valid JSON object with required non-null `translated_text` string.
2. **Non-Empty Output**: `!string.IsNullOrWhiteSpace(translatedText)` when source is non-empty.
3. **HTML Tag Parity**: Extract HTML tags from source and translation; verify tag names and counts match.
4. **Placeholder Parity**: Verify tokens like `{name}`, `{{count}}`, `%s`, `{0}` appear identically in translation.
5. **Glossary Compliance**: Verify every `glossary_terms.preferred_translation` matches and no `forbidden_translation` appears.
6. **Abnormal Length Guard**: Flag if translation length exceeds threshold (e.g., > 3x source length or < 0.25x for non-trivial text).
7. **Source Version Currency**: Confirm `source_version_id` is still the latest version before committing results.

Optional **Semantic Validation** (meaning, tone, fluency) must be feature-flagged and **never forced** as a second LLM call when local GPU throughput is constrained.

## 3. Optional Redis Cache (`IDistributedCache`)

- Redis is **optional** (`Instruction.md` caveat). All application services MUST continue working via PostgreSQL if Redis is unconfigured or temporarily unreachable.
- Prefix every cache key with tenant ID to prevent cross-tenant leakage:
  `optiflow:t:{tenantId}:tm:{sourceLocale}:{targetLocale}:{sourceHash}:{glossaryVersion}:{contentType}`
- Never cache unapproved (`AI_GENERATED` / `PENDING`) translations in TM cache.

## 4. OpenTelemetry & Redacted Logging

- Use `ActivitySource("OptiFlow.Api")`, `ActivitySource("OptiFlow.Worker")`, and `ActivitySource("OptiFlow.AI")`.
- Propagate W3C `traceparent` / `TraceId` from HTTP ingestion into `localization_jobs` (or `outbox_messages`) so the Worker continues the distributed trace asynchronously.
- Emit safe span tags & metrics only:
  - Allowed tags: `tenant.id`, `job.id`, `content.item_id`, `block.key`, `block.action`, `ai.model`, `ai.input_tokens`, `ai.output_tokens`, `tm.hit`.
  - **FORBIDDEN**: `source_text`, `translated_text`, prompts, raw payloads, credentials.
- Configure `Serilog` / `ILogger<T>` to log only IDs, SHA-256 hashes, durations, token counts, and error codes.
