# ADR-004: Stable Block Identity + SHA-256 Diff Engine

## Status
Accepted

## Context
Reordering blocks or insignificant whitespace edits should not invalidate existing translations or trigger redundant LLM calls.

## Decision
Normalize whitespace uniformly (`\r\n` → `\n`, collapse whitespace runs, trim), hash normalized block text with SHA-256 (`ContentHash`), and match blocks across versions by stable CMS `BlockKey` first with `FieldPath` fallback.

## Consequences
- Reordered or whitespace-reformatted blocks resolve as `Unchanged` with zero AI calls.
- Added, modified, and deleted blocks are tracked deterministically per block rather than retranslating full pages.
