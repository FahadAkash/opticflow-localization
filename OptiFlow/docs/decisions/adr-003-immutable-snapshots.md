# ADR-003: Immutable Content Snapshots

## Status
Accepted

## Context
Mutating source content versions in place makes historical diffs, auditability, and stale-version conflict detection unreliable.

## Decision
Treat `content_versions` and `content_blocks` as append-only immutable snapshots keyed by `UNIQUE(content_item_id, external_version_key, locale)`, backed by a PostgreSQL `BEFORE UPDATE` trigger (`prevent_version_update()`).

## Consequences
- Re-syncing an existing version is idempotent and returns the existing snapshot without overwriting history.
- Every localization job links to an exact immutable `source_version_id` for deterministic validation and diffing.
