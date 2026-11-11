---
name: postgresql-patterns
description: >-
  Use this skill when writing PostgreSQL migrations, job queue queries
  (FOR UPDATE SKIP LOCKED), JSONB operations, Row-Level Security policies,
  or composite unique constraints for OptiFlow.
---

# PostgreSQL Patterns for OptiFlow

## 1. Job Queue with SKIP LOCKED

The core pattern for claiming jobs without contention:

```sql
-- Claim the next available job
WITH next_job AS (
    SELECT id
    FROM localization_jobs
    WHERE (
            (status = 'QUEUED' AND available_at <= NOW())
         OR (status = 'PROCESSING' AND lease_expires_at < NOW())
          )
      AND attempt_count < max_attempts
    ORDER BY priority DESC, created_at ASC
    FOR UPDATE SKIP LOCKED
    LIMIT 1
)
UPDATE localization_jobs j
SET status           = 'PROCESSING',
    locked_by        = @worker_id,
    locked_at        = NOW(),
    lease_expires_at = NOW() + INTERVAL '5 minutes',
    attempt_count    = j.attempt_count + 1
FROM next_job
WHERE j.id = next_job.id
RETURNING j.*;
```

### Key properties:
- `SKIP LOCKED` prevents two workers from blocking on the same row.
- Expired leases (`lease_expires_at < NOW()`) are automatically reclaimed.
- `attempt_count < max_attempts` prevents infinite retries.
- `priority DESC, created_at ASC` ensures higher-priority jobs go first (FIFO within same priority).

### EF Core implementation:

```csharp
public async Task<LocalizationJob?> ClaimNextAsync(
    string workerId, CancellationToken cancellationToken)
{
    // Use raw SQL for SKIP LOCKED — EF Core doesn't support it natively
    var job = await _context.LocalizationJobs
        .FromSqlRaw("""
            WITH next_job AS (
                SELECT id FROM localization_jobs
                WHERE ((status = 'QUEUED' AND available_at <= NOW())
                    OR (status = 'PROCESSING' AND lease_expires_at < NOW()))
                  AND attempt_count < max_attempts
                ORDER BY priority DESC, created_at ASC
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE localization_jobs j
            SET status = 'PROCESSING',
                locked_by = {0},
                locked_at = NOW(),
                lease_expires_at = NOW() + INTERVAL '5 minutes',
                attempt_count = j.attempt_count + 1
            FROM next_job
            WHERE j.id = next_job.id
            RETURNING j.*
            """, workerId)
        .FirstOrDefaultAsync(cancellationToken);

    return job;
}
```

## 2. Immutable Snapshot Tables

Content versions are insert-only:

```sql
CREATE TABLE content_versions (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    content_item_id       UUID NOT NULL REFERENCES content_items(id),
    external_version_key  VARCHAR(500) NOT NULL,
    locale                VARCHAR(20) NOT NULL,
    version_number        INTEGER NOT NULL,
    content_hash          VARCHAR(64) NOT NULL,  -- SHA-256 hex
    raw_payload           JSONB NOT NULL,
    captured_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_version UNIQUE (content_item_id, external_version_key, locale)
);

-- NEVER UPDATE this table. New version = new row.
-- Add a trigger to enforce immutability:
CREATE OR REPLACE FUNCTION prevent_version_update()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'content_versions rows are immutable — insert a new row instead';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_content_versions_no_update
    BEFORE UPDATE ON content_versions
    FOR EACH ROW
    EXECUTE FUNCTION prevent_version_update();
```

### EF Core migration equivalent:

```csharp
migrationBuilder.Sql("""
    CREATE OR REPLACE FUNCTION prevent_version_update()
    RETURNS TRIGGER AS $$
    BEGIN
        RAISE EXCEPTION 'content_versions rows are immutable';
    END;
    $$ LANGUAGE plpgsql;

    CREATE TRIGGER trg_content_versions_no_update
        BEFORE UPDATE ON content_versions
        FOR EACH ROW
        EXECUTE FUNCTION prevent_version_update();
    """);
```

## 3. JSONB Column Conventions

Only three approved JSONB use cases:

| Column | Table | Contains |
|---|---|---|
| `raw_payload` | `content_versions` | Full CMS content snapshot (opaque) |
| `validation_result` | `localization_job_blocks` | Validation check breakdown |
| `metadata` | `audit_events` | Action-specific audit details |

### Querying JSONB (when needed):

```sql
-- Check a specific field inside the validation result
SELECT id, validation_result->>'overall_status' AS status
FROM localization_job_blocks
WHERE validation_result->>'overall_status' = 'FAILED';

-- Index for JSONB lookups (only if query patterns justify it)
CREATE INDEX idx_job_blocks_validation_status
ON localization_job_blocks ((validation_result->>'overall_status'));
```

### EF Core JSONB mapping:

```csharp
builder.Property(x => x.RawPayload)
    .HasColumnType("jsonb")
    .HasColumnName("raw_payload");

// For typed JSONB (validation results):
builder.OwnsOne(x => x.ValidationResult, vr =>
{
    vr.ToJson("validation_result");
});
```

## 4. Composite Unique Constraints

```sql
-- Webhook deduplication
CREATE UNIQUE INDEX uq_webhook_inbox
ON webhook_inbox (tenant_id, provider, external_event_id);

-- Translation memory lookup key
CREATE UNIQUE INDEX uq_translation_memory
ON translation_memory_entries (
    tenant_id, source_locale, target_locale,
    source_hash, glossary_version, content_type
);

-- Content block identity
CREATE UNIQUE INDEX uq_content_block
ON content_blocks (content_version_id, block_key);

-- Also add a hash lookup index for diff engine
CREATE INDEX idx_content_blocks_hash
ON content_blocks (content_version_id, content_hash);
```

## 5. Row-Level Security (Tenant Isolation)

```sql
-- Enable RLS on tenant-scoped tables
ALTER TABLE content_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE localization_jobs ENABLE ROW LEVEL SECURITY;

-- Policy: app user can only see rows for their tenant
CREATE POLICY tenant_isolation ON content_items
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);

CREATE POLICY tenant_isolation ON localization_jobs
    USING (tenant_id = current_setting('app.current_tenant_id')::uuid);
```

### Setting tenant context per request (EF Core):

```csharp
// In a middleware or DbContext factory
await using var conn = await dataSource.OpenConnectionAsync(ct);
await using var cmd = conn.CreateCommand();
cmd.CommandText = "SET app.current_tenant_id = @tid";
cmd.Parameters.Add(new NpgsqlParameter("tid", tenantId.Value));
await cmd.ExecuteNonQueryAsync(ct);
```

## 6. Idempotent Webhook Ingestion

```sql
-- Insert-or-skip pattern for webhook deduplication
INSERT INTO webhook_inbox (
    tenant_id, provider, external_event_id,
    payload_hash, processing_status, received_at
)
VALUES (@tenant_id, @provider, @external_event_id,
        @payload_hash, 'PENDING', NOW())
ON CONFLICT (tenant_id, provider, external_event_id)
DO NOTHING
RETURNING id;

-- If RETURNING is empty, it was a duplicate — skip processing.
```

## 7. Migration Best Practices

- Name migrations descriptively: `AddContentItemsTable`, `AddJobQueueLeaseColumns`
- Always run `dotnet ef migrations script` to review generated SQL before applying
- Use `migrationBuilder.Sql()` for triggers, functions, and RLS policies
- Keep migrations forward-only — never edit a migration that has been applied
- Add `DOWN` migration (revert) logic for non-trivial changes
