using Microsoft.EntityFrameworkCore;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Infrastructure.Persistence;

public sealed class OptiFlowRepository : IOptiFlowRepository
{
    private readonly OptiFlowDbContext _db;

    public OptiFlowRepository(OptiFlowDbContext db)
    {
        _db = db;
    }

    public async Task<Tenant?> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);

    public async Task AddTenantAsync(Tenant tenant, CancellationToken cancellationToken) =>
        await _db.Tenants.AddAsync(tenant, cancellationToken).ConfigureAwait(false);

    public async Task<CmsConnection?> GetConnectionAsync(TenantId tenantId, ConnectionId connectionId, CancellationToken cancellationToken) =>
        await _db.CmsConnections
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == connectionId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<CmsConnection>> ListConnectionsAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await _db.CmsConnections
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddConnectionAsync(CmsConnection connection, CancellationToken cancellationToken) =>
        await _db.CmsConnections.AddAsync(connection, cancellationToken).ConfigureAwait(false);

    public async Task<ContentItem?> GetContentItemAsync(TenantId tenantId, ContentItemId contentItemId, CancellationToken cancellationToken) =>
        await _db.ContentItems
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == contentItemId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<ContentItem?> GetContentItemByExternalKeyAsync(
        TenantId tenantId,
        ConnectionId connectionId,
        ContentKey externalKey,
        CancellationToken cancellationToken) =>
        await _db.ContentItems
            .FirstOrDefaultAsync(
                i => i.TenantId == tenantId && i.ConnectionId == connectionId && i.ExternalContentKey == externalKey,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ContentItem>> ListContentItemsAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await _db.ContentItems
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddContentItemAsync(ContentItem item, CancellationToken cancellationToken) =>
        await _db.ContentItems.AddAsync(item, cancellationToken).ConfigureAwait(false);

    public async Task<ContentVersion?> GetContentVersionAsync(TenantId tenantId, ContentVersionId versionId, CancellationToken cancellationToken) =>
        await _db.ContentVersions
            .Include(v => v.Blocks)
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Id == versionId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<ContentVersion?> GetContentVersionByExternalKeyAsync(
        TenantId tenantId,
        ContentItemId itemId,
        string externalVersionKey,
        LocaleCode locale,
        CancellationToken cancellationToken) =>
        await _db.ContentVersions
            .Include(v => v.Blocks)
            .FirstOrDefaultAsync(
                v => v.TenantId == tenantId &&
                     v.ContentItemId == itemId &&
                     v.ExternalVersionKey == externalVersionKey &&
                     v.Locale == locale,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ContentVersion>> ListContentVersionsAsync(
        TenantId tenantId,
        ContentItemId itemId,
        CancellationToken cancellationToken) =>
        await _db.ContentVersions
            .Include(v => v.Blocks)
            .Where(v => v.TenantId == tenantId && v.ContentItemId == itemId)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddContentVersionAsync(ContentVersion version, CancellationToken cancellationToken) =>
        await _db.ContentVersions.AddAsync(version, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<string, Translation>> GetTranslationsAsync(
        TenantId tenantId,
        ContentItemId itemId,
        LocaleCode targetLocale,
        CancellationToken cancellationToken)
    {
        List<Translation> rows = await _db.Translations
            .Where(t => t.TenantId == tenantId && t.ContentItemId == itemId && t.TargetLocale == targetLocale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(t => t.BlockKey.Value, t => t, StringComparer.Ordinal);
    }

    public async Task UpsertTranslationAsync(Translation translation, CancellationToken cancellationToken) =>
        await _db.Translations.AddAsync(translation, cancellationToken).ConfigureAwait(false);

    public async Task<TranslationMemoryEntry?> FindApprovedTranslationMemoryAsync(
        TranslationMemoryKey key,
        CancellationToken cancellationToken)
    {
        var sourceLocale = new LocaleCode(key.SourceLocale);
        var targetLocale = new LocaleCode(key.TargetLocale);
        return await _db.TranslationMemoryEntries
            .FirstOrDefaultAsync(
                tm => tm.TenantId == key.TenantId &&
                      tm.SourceLocale == sourceLocale &&
                      tm.TargetLocale == targetLocale &&
                      tm.SourceHash == key.SourceHash &&
                      tm.GlossaryVersion == key.GlossaryVersion &&
                      tm.ContentType == key.ContentType &&
                      tm.ApprovalStatus == ApprovalStatus.Approved,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TranslationMemoryEntry?> FindTranslationMemoryByCompositeKeyAsync(
        TranslationMemoryKey key,
        CancellationToken cancellationToken)
    {
        var sourceLocale = new LocaleCode(key.SourceLocale);
        var targetLocale = new LocaleCode(key.TargetLocale);
        return await _db.TranslationMemoryEntries
            .FirstOrDefaultAsync(
                tm => tm.TenantId == key.TenantId &&
                      tm.SourceLocale == sourceLocale &&
                      tm.TargetLocale == targetLocale &&
                      tm.SourceHash == key.SourceHash &&
                      tm.GlossaryVersion == key.GlossaryVersion &&
                      tm.ContentType == key.ContentType,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddTranslationMemoryEntryAsync(TranslationMemoryEntry entry, CancellationToken cancellationToken) =>
        await _db.TranslationMemoryEntries.AddAsync(entry, cancellationToken).ConfigureAwait(false);

    public async Task<LocalizationJob?> GetJobAsync(TenantId tenantId, JobId jobId, CancellationToken cancellationToken) =>
        await _db.LocalizationJobs
            .Include(j => j.Blocks)
            .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == jobId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<LocalizationJob?> GetJobByIdAsync(JobId jobId, CancellationToken cancellationToken) =>
        await _db.LocalizationJobs
            .IgnoreQueryFilters()
            .Include(j => j.Blocks)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<LocalizationJob?> GetJobByIdempotencyKeyAsync(
        TenantId tenantId,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken) =>
        await _db.LocalizationJobs
            .Include(j => j.Blocks)
            .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<LocalizationJob>> ListJobsAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await _db.LocalizationJobs
            .Include(j => j.Blocks)
            .Where(j => j.TenantId == tenantId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddJobAsync(LocalizationJob job, CancellationToken cancellationToken) =>
        await _db.LocalizationJobs.AddAsync(job, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<GlossaryTerm>> GetGlossaryTermsAsync(
        TenantId tenantId,
        LocaleCode sourceLocale,
        LocaleCode targetLocale,
        string glossaryVersion,
        CancellationToken cancellationToken) =>
        await _db.GlossaryTerms
            .Where(g => g.TenantId == tenantId &&
                        g.SourceLocale == sourceLocale &&
                        g.TargetLocale == targetLocale &&
                        g.GlossaryVersion == glossaryVersion)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddGlossaryTermAsync(GlossaryTerm term, CancellationToken cancellationToken) =>
        await _db.GlossaryTerms.AddAsync(term, cancellationToken).ConfigureAwait(false);

    public async Task<ReviewTask?> GetReviewTaskAsync(TenantId tenantId, ReviewTaskId reviewTaskId, CancellationToken cancellationToken) =>
        await _db.ReviewTasks
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == reviewTaskId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<ReviewTask?> GetReviewTaskByJobAsync(TenantId tenantId, JobId jobId, CancellationToken cancellationToken) =>
        await _db.ReviewTasks
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.JobId == jobId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ReviewTask>> ListPendingReviewsAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await _db.ReviewTasks
            .Where(r => r.TenantId == tenantId && r.Status == ReviewStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddReviewTaskAsync(ReviewTask task, CancellationToken cancellationToken) =>
        await _db.ReviewTasks.AddAsync(task, cancellationToken).ConfigureAwait(false);

    public async Task AddAiInvocationAsync(AiInvocation invocation, CancellationToken cancellationToken) =>
        await _db.AiInvocations.AddAsync(invocation, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<AiInvocation>> ListAiInvocationsAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await _db.AiInvocations
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<WebhookInbox?> GetWebhookAsync(
        TenantId tenantId,
        string provider,
        string externalEventId,
        CancellationToken cancellationToken) =>
        await _db.WebhookInbox
            .FirstOrDefaultAsync(
                w => w.TenantId == tenantId && w.Provider == provider && w.ExternalEventId == externalEventId,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task AddWebhookAsync(WebhookInbox webhook, CancellationToken cancellationToken) =>
        await _db.WebhookInbox.AddAsync(webhook, cancellationToken).ConfigureAwait(false);

    public async Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
        await _db.AuditEvents.AddAsync(auditEvent, cancellationToken).ConfigureAwait(false);

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        await _db.OutboxMessages.AddAsync(message, cancellationToken).ConfigureAwait(false);
}
