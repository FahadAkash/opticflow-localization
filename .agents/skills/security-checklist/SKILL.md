---
name: security-checklist
description: >-
  Use this skill before code review or when implementing authentication,
  authorization, tenant isolation, credential handling, or input validation
  in OptiFlow. Contains verification checklists for every security domain.
---

# OptiFlow Security Checklist

## 1. Tenant Isolation Verification

Before any PR that touches data access:

- [ ] Every query includes `WHERE tenant_id = @tenantId`
- [ ] No `SELECT *` without tenant filter in the WHERE clause
- [ ] EF Core global query filters applied for tenant-scoped entities
- [ ] Row-Level Security policies active on production tables
- [ ] API endpoints extract `tenant_id` from authenticated claims, never from
      request body or query string
- [ ] Background jobs carry `tenant_id` in the job record, not resolved at
      processing time
- [ ] Redis cache keys include tenant prefix: `tenant:{tenantId}:...`
- [ ] Log correlation includes tenant ID for cross-reference

### EF Core Global Filter Pattern

```csharp
// In DbContext.OnModelCreating
modelBuilder.Entity<ContentItem>()
    .HasQueryFilter(x => x.TenantId == _currentTenantId);
```

## 2. Credential Handling

- [ ] `cms_connections.credential_ref` stores a vault reference, never a
      plaintext secret
- [ ] Connection strings use environment variables or User Secrets locally
- [ ] No secrets in `appsettings.json` or `appsettings.Development.json`
- [ ] `.gitignore` excludes `appsettings.*.local.json`, `secrets.json`
- [ ] API keys rotated via configuration — no code changes needed
- [ ] Credential access logged in audit trail (who accessed which connection)

### Local Development Setup

```bash
# Use dotnet user-secrets for local development
cd src/OptiFlow.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=optiflow;Username=dev;Password=devpass"
dotnet user-secrets set "LMStudio:BaseUrl" "http://localhost:1234"
dotnet user-secrets set "Optimizely:ApiKey" "your-dev-key-here"
```

## 3. Input Validation

### API Request Validation

- [ ] All request DTOs have validation attributes or FluentValidation rules
- [ ] String lengths are bounded (no unbounded text fields)
- [ ] Enum values are validated (reject unknown values)
- [ ] Nested objects are recursively validated
- [ ] File uploads (if any) have size and type restrictions

### Webhook Payload Validation

- [ ] Verify webhook signature (HMAC or similar)
- [ ] Check `payload_hash` for integrity
- [ ] Deduplicate via `webhook_inbox` unique constraint
- [ ] Reject payloads exceeding size limit
- [ ] Validate JSON schema before processing
- [ ] Rate-limit webhook endpoint

### CMS Response Validation

- [ ] Null-check all fields before use
- [ ] Validate content structure matches expected schema
- [ ] Handle partial responses gracefully
- [ ] Timeout external calls (default: 30 seconds)

## 4. Logging & Data Redaction

### What to Log (IDs, metrics, metadata)

```csharp
_logger.LogInformation(
    "Processing job {JobId} for tenant {TenantId}, " +
    "content {ContentKey}, {BlockCount} blocks",
    jobId, tenantId, contentKey, blockCount);
```

### What NEVER to Log

- ❌ Source text or translated text
- ❌ AI prompts or AI responses
- ❌ Raw CMS payloads (content)
- ❌ Connection strings or credentials
- ❌ JWT tokens or session tokens
- ❌ User passwords or PII
- ❌ Stack traces in production API responses

### Redaction Pattern

```csharp
// For debugging — log hash, not content
_logger.LogDebug(
    "Block {BlockKey} content hash: {ContentHash}",
    blockKey, contentHash);

// NEVER this:
// _logger.LogDebug("Block text: {Text}", sourceText);  // ❌
```

## 5. CMS Draft-Only Rule

- [ ] `IContentSource.CreateDraftAsync` creates a DRAFT, never publishes
- [ ] The `expectedVersion` parameter enforces optimistic concurrency (ETag)
- [ ] Version mismatch → `STALE_CONFLICT` state, re-fetch and re-diff
- [ ] No code path exists that calls a publish/activate endpoint
- [ ] Draft creation requires a preceding `APPROVED` review task
- [ ] Audit event recorded for every draft creation

```csharp
// Verify before draft creation
if (job.Status != JobStatus.Approved)
    return Result<DraftResult>.Failure(
        "Cannot create draft — job not approved", "NOT_APPROVED");

var result = await _contentSource.CreateDraftAsync(
    draftContent, job.ExpectedVersion, cancellationToken);

if (result.IsVersionMismatch)
{
    await _jobRepository.UpdateStatusAsync(
        job.Id, JobStatus.StaleConflict, cancellationToken);
    return Result<DraftResult>.Failure(
        "CMS version changed", "STALE_CONFLICT");
}
```

## 6. Translation Memory Approval Rule

- [ ] Only entries with `approval_status = 'APPROVED'` are returned by
      `FindApprovedAsync`
- [ ] AI-generated translations are saved with `provenance = 'AI_GENERATED'`
      and `approval_status = 'PENDING'`
- [ ] No code path auto-promotes `PENDING` → `APPROVED`
- [ ] Only a human reviewer action sets `approval_status = 'APPROVED'`
- [ ] Provenance is always set: `AI_GENERATED`, `HUMAN_EDITED`, or
      `HUMAN_APPROVED`

## 7. AI Output Safety

- [ ] Every AI response is validated deterministically before use
- [ ] JSON schema validation (reject malformed responses)
- [ ] Non-empty check on `translated_text`
- [ ] HTML tag preservation check
- [ ] Placeholder preservation check (`{name}`, `{{count}}`)
- [ ] Glossary term compliance check
- [ ] Length ratio check (reject if > 3× source length)
- [ ] Source version currency check (is the source still current?)
- [ ] Failed validation → retry with backoff (max 4 attempts)
- [ ] All retries exhausted → `DEAD_LETTER`, never silent skip
- [ ] Model `confidence` is logged but NEVER used as a gate

## 8. Error Response Safety

```csharp
// API error responses — never expose internals
public sealed record ApiError(string Message, string Code)
{
    // Factory methods for standard errors
    public static ApiError NotFound(string resource)
        => new($"{resource} not found", "NOT_FOUND");

    public static ApiError Conflict(string reason)
        => new(reason, "CONFLICT");

    public static ApiError Internal()
        => new("An internal error occurred", "INTERNAL_ERROR");
    // Never: new("NpgsqlException: connection refused at 10.0.0.5:5432", ...)
}
```
