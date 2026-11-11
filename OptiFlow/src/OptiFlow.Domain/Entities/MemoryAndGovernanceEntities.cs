using OptiFlow.Domain.Enums;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Domain.Entities;

public sealed class TranslationMemoryEntry
{
    public TranslationMemoryId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public LocaleCode SourceLocale { get; private set; } = null!;
    public LocaleCode TargetLocale { get; private set; } = null!;
    public ContentHash SourceHash { get; private set; } = null!;
    public string TranslatedText { get; private set; } = string.Empty;
    public TranslationProvenance Provenance { get; private set; }
    public ApprovalStatus ApprovalStatus { get; private set; }
    public string GlossaryVersion { get; private set; } = "v1";
    public string ContentType { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ApprovedBy { get; private set; }

    private TranslationMemoryEntry() { }

    private TranslationMemoryEntry(
        TranslationMemoryId id,
        TenantId tenantId,
        LocaleCode sourceLocale,
        LocaleCode targetLocale,
        ContentHash sourceHash,
        string translatedText,
        TranslationProvenance provenance,
        ApprovalStatus approvalStatus,
        string glossaryVersion,
        string contentType,
        DateTimeOffset createdAt,
        DateTimeOffset? approvedAt,
        string? approvedBy)
    {
        Id = id;
        TenantId = tenantId;
        SourceLocale = sourceLocale;
        TargetLocale = targetLocale;
        SourceHash = sourceHash;
        TranslatedText = translatedText;
        Provenance = provenance;
        ApprovalStatus = approvalStatus;
        GlossaryVersion = glossaryVersion;
        ContentType = contentType;
        CreatedAt = createdAt;
        ApprovedAt = approvedAt;
        ApprovedBy = approvedBy;
    }

    public static TranslationMemoryEntry CreatePendingAiCandidate(
        TranslationMemoryId id,
        TenantId tenantId,
        LocaleCode sourceLocale,
        LocaleCode targetLocale,
        ContentHash sourceHash,
        string translatedText,
        string glossaryVersion,
        string contentType,
        DateTimeOffset createdAt) =>
        new(
            id,
            tenantId,
            sourceLocale,
            targetLocale,
            sourceHash,
            translatedText,
            TranslationProvenance.AiGenerated,
            ApprovalStatus.Pending,
            glossaryVersion,
            contentType,
            createdAt,
            null,
            null);

    public static TranslationMemoryEntry CreateApproved(
        TranslationMemoryId id,
        TenantId tenantId,
        LocaleCode sourceLocale,
        LocaleCode targetLocale,
        ContentHash sourceHash,
        string translatedText,
        string glossaryVersion,
        string contentType,
        string approvedBy,
        DateTimeOffset approvedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);
        return new(
            id,
            tenantId,
            sourceLocale,
            targetLocale,
            sourceHash,
            translatedText,
            TranslationProvenance.HumanApproved,
            ApprovalStatus.Approved,
            glossaryVersion,
            contentType,
            approvedAt,
            approvedAt,
            approvedBy.Trim());
    }

    public void Approve(string approvedText, string reviewerId, DateTimeOffset approvedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);
        TranslatedText = approvedText;
        Provenance = TranslationProvenance.HumanApproved;
        ApprovalStatus = ApprovalStatus.Approved;
        ApprovedBy = reviewerId.Trim();
        ApprovedAt = approvedAt;
    }
}

public sealed class GlossaryTerm
{
    public GlossaryTermId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public LocaleCode SourceLocale { get; private set; } = null!;
    public LocaleCode TargetLocale { get; private set; } = null!;
    public string GlossaryVersion { get; private set; } = "v1";
    public string SourceTerm { get; private set; } = string.Empty;
    public string PreferredTranslation { get; private set; } = string.Empty;
    public string? ForbiddenTranslation { get; private set; }

    private GlossaryTerm() { }

    public GlossaryTerm(
        GlossaryTermId id,
        TenantId tenantId,
        LocaleCode sourceLocale,
        LocaleCode targetLocale,
        string glossaryVersion,
        string sourceTerm,
        string preferredTranslation,
        string? forbiddenTranslation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(glossaryVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTerm);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredTranslation);

        Id = id;
        TenantId = tenantId;
        SourceLocale = sourceLocale;
        TargetLocale = targetLocale;
        GlossaryVersion = glossaryVersion.Trim();
        SourceTerm = sourceTerm.Trim();
        PreferredTranslation = preferredTranslation.Trim();
        ForbiddenTranslation = string.IsNullOrWhiteSpace(forbiddenTranslation) ? null : forbiddenTranslation.Trim();
    }
}

public sealed class ReviewTask
{
    public ReviewTaskId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public JobId JobId { get; private set; }
    public string? ReviewerId { get; private set; }
    public ReviewStatus Status { get; private set; }
    public string? Decision { get; private set; }
    public string? Comments { get; private set; }
    public string? ExpectedCmsVersion { get; private set; }
    public string? CmsDraftId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    private ReviewTask() { }

    public ReviewTask(
        ReviewTaskId id,
        TenantId tenantId,
        JobId jobId,
        string expectedCmsVersion,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        JobId = jobId;
        Status = ReviewStatus.Pending;
        ExpectedCmsVersion = expectedCmsVersion;
        CreatedAt = createdAt;
    }

    public void Approve(string reviewerId, string? comments, string? cmsDraftId, DateTimeOffset reviewedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);
        ReviewerId = reviewerId.Trim();
        Status = ReviewStatus.Approved;
        Decision = "APPROVED";
        Comments = comments;
        CmsDraftId = cmsDraftId;
        ReviewedAt = reviewedAt;
    }

    public void Reject(string reviewerId, string? comments, DateTimeOffset reviewedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);
        ReviewerId = reviewerId.Trim();
        Status = ReviewStatus.Rejected;
        Decision = "REJECTED";
        Comments = comments;
        ReviewedAt = reviewedAt;
    }
}

public sealed class AiInvocation
{
    public AiInvocationId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public JobId JobId { get; private set; }
    public JobBlockId JobBlockId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ModelName { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int LatencyMs { get; private set; }
    public decimal EstimatedCost { get; private set; }
    public AiInvocationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AiInvocation() { }

    public AiInvocation(
        AiInvocationId id,
        TenantId tenantId,
        JobId jobId,
        JobBlockId jobBlockId,
        string provider,
        string modelName,
        string promptVersion,
        int inputTokens,
        int outputTokens,
        int latencyMs,
        decimal estimatedCost,
        AiInvocationStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        JobId = jobId;
        JobBlockId = jobBlockId;
        Provider = provider;
        ModelName = modelName;
        PromptVersion = promptVersion;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        LatencyMs = latencyMs;
        EstimatedCost = estimatedCost;
        Status = status;
        CreatedAt = createdAt;
    }
}

public sealed class AuditEvent
{
    public AuditEventId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public string ResourceId { get; private set; } = string.Empty;
    public string Metadata { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }

    private AuditEvent() { }

    public AuditEvent(
        AuditEventId id,
        TenantId tenantId,
        string actorId,
        string action,
        string resourceType,
        string resourceId,
        string metadataJson,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ActorId = actorId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Metadata = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
        CreatedAt = createdAt;
    }
}

public sealed class WebhookInbox
{
    public WebhookInboxId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ExternalEventId { get; private set; } = string.Empty;
    public ContentHash PayloadHash { get; private set; } = null!;
    public WebhookProcessingStatus ProcessingStatus { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private WebhookInbox() { }

    public WebhookInbox(
        WebhookInboxId id,
        TenantId tenantId,
        string provider,
        string externalEventId,
        ContentHash payloadHash,
        DateTimeOffset receivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalEventId);

        Id = id;
        TenantId = tenantId;
        Provider = provider.Trim();
        ExternalEventId = externalEventId.Trim();
        PayloadHash = payloadHash;
        ProcessingStatus = WebhookProcessingStatus.Pending;
        ReceivedAt = receivedAt;
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessingStatus = WebhookProcessingStatus.Processed;
        ProcessedAt = processedAt;
    }

    public void MarkFailed(DateTimeOffset processedAt)
    {
        ProcessingStatus = WebhookProcessingStatus.Failed;
        ProcessedAt = processedAt;
    }
}

public sealed class OutboxMessage
{
    public OutboxMessageId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = "{}";
    public OutboxStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(
        OutboxMessageId id,
        TenantId tenantId,
        Guid aggregateId,
        string eventType,
        string payload,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        AggregateId = aggregateId;
        EventType = eventType;
        Payload = payload;
        Status = OutboxStatus.Pending;
        CreatedAt = createdAt;
    }

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        Status = OutboxStatus.Published;
        PublishedAt = publishedAt;
    }
}
