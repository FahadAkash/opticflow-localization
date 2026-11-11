# ADR-009: OpenTelemetry End-to-End Tracing & Redacted Logging

## Status
Accepted

## Context
Asynchronous API-to-Worker localization pipelines require end-to-end correlation and latency/token visibility without leaking customer content into logs or traces.

## Decision
Emit OpenTelemetry traces (`OptiFlow.*`) and metrics (`optiflow.ai.tokens`, `optiflow.tm.hits`, `optiflow.ai.latency_ms`) carrying `tenant_id`, `job_id`, SHA-256 hashes, and token counts only, alongside Serilog structured logging.

## Consequences
- Full distributed correlation across API, Worker, and LM Studio calls exportable to Grafana via OTLP.
- Raw customer source text, translated text, and LLM prompts are excluded from logs and telemetry tags.
