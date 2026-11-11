# ADR-006: Local LLM via Adapter Pattern & Deterministic Validation

## Status
Accepted

## Context
Running localization against a local quantized Gemma 4B model in LM Studio on 8 GB VRAM requires bounded concurrency, strict JSON contracts, and reliable guardrails against malformed outputs.

## Decision
Encapsulate LLM calls behind `ILanguageModelClient` (`LmStudioLanguageModelClient`) with default `MaxConcurrency = 1`, strict JSON parsing, `ai_invocations` token logging, and 7 mandatory deterministic checks before optional semantic checks.

## Consequences
- Model `confidence` is logged for analysis but never used as an approval gate.
- Swapping LM Studio for a cloud LLM provider requires implementing `ILanguageModelClient` with zero changes to Domain or Application logic.
