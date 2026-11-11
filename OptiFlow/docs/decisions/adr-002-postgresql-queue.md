# ADR-002: PostgreSQL Job Queue (`FOR UPDATE SKIP LOCKED`)

## Status
Accepted

## Context
API ingestion (`POST /api/v1/localization/jobs` and webhooks) must commit job creation atomically with `webhook_inbox` and `outbox_messages` without introducing Kafka or RabbitMQ.

## Decision
Use PostgreSQL `localization_jobs` as the job queue via `FOR UPDATE SKIP LOCKED`, priority/FIFO ordering, a 5-minute lease (`lease_expires_at`), and exponential retry delays (`0s`, `2s`, `4s`, `8s`, then `DEAD_LETTER`).

## Consequences
- Event ingestion and job queueing commit in a single ACID transaction.
- Expired worker leases are automatically reclaimed after worker crashes without an external broker.
- External AI invocations remain at-least-once, requiring idempotent block storage.
