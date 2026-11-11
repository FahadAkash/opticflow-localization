@echo off
echo {"injectSteps":[{"systemMessage":{"systemMessage":"OptiFlow invariants: (1) Domain has zero infra refs. (2) Tenant-scope ALL queries. (3) Never log raw content. (4) Never auto-publish - drafts only after approval. (5) Never auto-approve TM entries. (6) Immutable snapshots. (7) Validate all LLM output deterministically. (8) Idempotent webhook ingestion. (9) ETag/version check before CMS draft. Update PROJECT_COST_AND_INFO.md and .agents/PROJECT_TRACKER.md at milestone completion."}}]}
exit /b 0
