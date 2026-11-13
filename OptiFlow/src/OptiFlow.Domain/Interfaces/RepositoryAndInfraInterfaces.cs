using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Domain.Interfaces;

public interface ITenantContext
{
    TenantId CurrentTenantId { get; }
    string CurrentActorId { get; }
    bool HasTenant { get; }
    void SetTenant(TenantId tenantId, string actorId);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(TenantId tenantId, string key, CancellationToken cancellationToken)
        where T : class;

    Task SetAsync<T>(TenantId tenantId, string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
        where T : class;

    Task RemoveAsync(TenantId tenantId, string key, CancellationToken cancellationToken);
}

public interface ICredentialProtector
{
    string Protect(string plaintext);
    string Unprotect(string credentialRef);
}

public interface IWebhookSignatureVerifier
{
    bool VerifySignature(string payload, string? signatureHeader, string secretRef);
}

public interface IJobQueue
{
    Task<LocalizationJob?> ClaimNextAsync(string workerId, CancellationToken cancellationToken);
}

public interface IJobNotificationPublisher
{
    Task PublishJobStatusChangedAsync(
        TenantId tenantId,
        JobId jobId,
        JobStatus status,
        CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IContentSourceResolver
{
    IContentSource Resolve(CmsProvider provider);
}

public interface IOptiFlowRepository
{
    Task<Tenant?> GetTenantAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task AddTenantAsync(Tenant tenant, CancellationToken cancellationToken);

    Task<CmsConnection?> GetConnectionAsync(TenantId tenantId, ConnectionId connectionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CmsConnection>> ListConnectionsAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task AddConnectionAsync(CmsConnection connection, CancellationToken cancellationToken);

    Task<ContentItem?> GetContentItemAsync(TenantId tenantId, ContentItemId contentItemId, CancellationToken cancellationToken);
    Task<ContentItem?> GetContentItemByExternalKeyAsync(TenantId tenantId, ConnectionId connectionId, ContentKey externalKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContentItem>> ListContentItemsAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task AddContentItemAsync(ContentItem item, CancellationToken cancellationToken);

    Task<ContentVersion?> GetContentVersionAsync(TenantId tenantId, ContentVersionId versionId, CancellationToken cancellationToken);
    Task<ContentVersion?> GetContentVersionByExternalKeyAsync(TenantId tenantId, ContentItemId itemId, string externalVersionKey, LocaleCode locale, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContentVersion>> ListContentVersionsAsync(TenantId tenantId, ContentItemId itemId, CancellationToken cancellationToken);
    Task AddContentVersionAsync(ContentVersion version, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, Translation>> GetTranslationsAsync(TenantId tenantId, ContentItemId itemId, LocaleCode targetLocale, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocaleCode>> GetAvailableTranslationLocalesAsync(TenantId tenantId, ContentItemId itemId, CancellationToken cancellationToken);
    Task UpsertTranslationAsync(Translation translation, CancellationToken cancellationToken);

    Task<TranslationMemoryEntry?> FindApprovedTranslationMemoryAsync(TranslationMemoryKey key, CancellationToken cancellationToken);
    Task<TranslationMemoryEntry?> FindTranslationMemoryByCompositeKeyAsync(TranslationMemoryKey key, CancellationToken cancellationToken);
    Task AddTranslationMemoryEntryAsync(TranslationMemoryEntry entry, CancellationToken cancellationToken);

    Task<LocalizationJob?> GetJobAsync(TenantId tenantId, JobId jobId, CancellationToken cancellationToken);
    Task<LocalizationJob?> GetJobByIdAsync(JobId jobId, CancellationToken cancellationToken);
    Task<LocalizationJob?> GetJobByIdempotencyKeyAsync(TenantId tenantId, IdempotencyKey idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocalizationJob>> ListJobsAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task AddJobAsync(LocalizationJob job, CancellationToken cancellationToken);

    Task<IReadOnlyList<GlossaryTerm>> GetGlossaryTermsAsync(TenantId tenantId, LocaleCode sourceLocale, LocaleCode targetLocale, string glossaryVersion, CancellationToken cancellationToken);
    Task AddGlossaryTermAsync(GlossaryTerm term, CancellationToken cancellationToken);

    Task<ReviewTask?> GetReviewTaskAsync(TenantId tenantId, ReviewTaskId reviewTaskId, CancellationToken cancellationToken);
    Task<ReviewTask?> GetReviewTaskByJobAsync(TenantId tenantId, JobId jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewTask>> ListPendingReviewsAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewTask>> ListReviewsAsync(TenantId tenantId, ReviewStatus? status, CancellationToken cancellationToken);
    Task AddReviewTaskAsync(ReviewTask task, CancellationToken cancellationToken);

    Task AddAiInvocationAsync(AiInvocation invocation, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiInvocation>> ListAiInvocationsAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<WebhookInbox?> GetWebhookAsync(TenantId tenantId, string provider, string externalEventId, CancellationToken cancellationToken);
    Task AddWebhookAsync(WebhookInbox webhook, CancellationToken cancellationToken);

    Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken);
}
