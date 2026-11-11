---
name: react-typescript-patterns
description: >-
  Use this skill when building or updating the OptiFlow React + TypeScript
  Operations and Review Dashboard in frontend/react-dashboard/. Covers the
  4 dashboard screens, typed API client, TanStack Query, SignalR real-time
  job updates, and XSS-safe translation rendering.
---

# React + TypeScript Patterns for OptiFlow Dashboard

## 1. Four Required Dashboard Screens

1. **Content Intelligence** — Visualize blocks per content version (`unchanged`, `modified`, `added`, `deleted`), SHA-256 hashes, and version-to-version diff breakdown.
2. **Localization Operations** — Live job queue monitor (`QUEUED`, `SNAPSHOTTING`, `DIFFING`, `TRANSLATING`, `VALIDATING`, `RETRY_WAIT`, `NEEDS_MANUAL_REVIEW`, `AWAITING_REVIEW`, `STALE_CONFLICT`, `FAILED`, `DEAD_LETTER`) with manual sync trigger and retry controls.
3. **Translation Review** — Side-by-side comparison showing:
   - Source text (original master block)
   - Previous translation (highlighting `HUMAN_EDITED` / `HUMAN_APPROVED` provenance)
   - Proposed AI / TM translation
   - Deterministic validation badges (HTML tags, placeholders, glossary, length, version currency)
   - Conflict resolution UI for modified + human-edited blocks
   - Explicit **Approve** / **Reject** actions (creating CMS draft only after approval; never auto-publish)
4. **Cost & Performance** — Translation Reuse Rate, AI Credit Reduction (full-page baseline vs. incremental tokens), Content Preservation Rate, Processing Latency (p50/p95/p99), Job Success Rate, and local vs. cloud cost comparison.

## 2. TypeScript & Project Standards

- Enable `"strict": true`, `"noUncheckedIndexedAccess": true`, `"noImplicitAny": true` in `tsconfig.json`.
- Mirror backend DTOs with immutable `readonly` TypeScript interfaces/types matching API JSON camelCase fields.
- Centralize all HTTP calls in `src/api/` using a typed client wrapper that normalizes `ApiError` responses (`{ message, code }`).
- Use **TanStack Query** (`@tanstack/react-query`) for server state, caching, and invalidation after review actions.

```typescript
export type JobStatus =
  | 'QUEUED'
  | 'SNAPSHOTTING'
  | 'DIFFING'
  | 'TRANSLATING'
  | 'VALIDATING'
  | 'RETRY_WAIT'
  | 'NEEDS_MANUAL_REVIEW'
  | 'AWAITING_REVIEW'
  | 'APPROVED'
  | 'REJECTED'
  | 'CMS_DRAFT_CREATED'
  | 'STALE_CONFLICT'
  | 'FAILED'
  | 'DEAD_LETTER';

export interface LocalizationJobSummary {
  readonly id: string;
  readonly contentItemId: string;
  readonly targetLocale: string;
  readonly status: JobStatus;
  readonly attemptCount: number;
  readonly createdAt: string;
}
```

## 3. Real-Time Updates (SignalR + Polling Fallback)

- Subscribe to job status updates via SignalR hub when connected; fall back gracefully to TanStack Query `refetchInterval` when jobs are in active states (`QUEUED`..`VALIDATING`).
- Stop polling once jobs reach terminal or waiting-for-human states (`AWAITING_REVIEW`, `CMS_DRAFT_CREATED`, `REJECTED`, `FAILED`, `DEAD_LETTER`).
- Always clean up SignalR connections inside `useEffect` return handlers.

## 4. Security Rules (Frontend)

- **XSS Prevention**: Translated CMS blocks may contain HTML. Render block diffs as escaped text by default, or sanitize strictly (e.g., DOMPurify allowlist) if a rich preview toggle is provided. NEVER pass raw CMS/LLM output to `dangerouslySetInnerHTML`.
- **No Secrets in Client Bundles**: Never store CMS credentials, API keys, or vault secrets in `VITE_*` environment variables. All CMS/AI calls go through `OptiFlow.Api`.
- **Optimistic Concurrency UX**: If `POST /api/v1/reviews/{id}/approve` returns `409 Conflict` (`STALE_CONFLICT`), show a clear banner explaining the CMS source changed and prompt re-diff rather than silently retrying.
