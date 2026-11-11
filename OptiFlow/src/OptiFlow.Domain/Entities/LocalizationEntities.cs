using OptiFlow.Domain.Common;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Domain.Entities;

public sealed class LocalizationJob
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8)
    ];

    private readonly List<LocalizationJobBlock> _blocks = [];

    public JobId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ContentItemId ContentItemId { get; private set; }
    public ContentVersionId SourceVersionId { get; private set; }
    public ContentVersionId? PreviousSourceVersionId { get; private set; }
    public LocaleCode TargetLocale { get; private set; } = null!;
    public JobStatus Status { get; private set; }
    public IdempotencyKey IdempotencyKey { get; private set; } = null!;
    public int Priority { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset AvailableAt { get; private set; }
    public string? LockedBy { get; private set; }
    public DateTimeOffset? LockedAt { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? TraceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyCollection<LocalizationJobBlock> Blocks => _blocks.AsReadOnly();

    private LocalizationJob() { }

    public LocalizationJob(
        JobId id,
        TenantId tenantId,
        ContentItemId contentItemId,
        ContentVersionId sourceVersionId,
        ContentVersionId? previousSourceVersionId,
        LocaleCode targetLocale,
        IdempotencyKey idempotencyKey,
        DateTimeOffset createdAt,
        int priority = 0,
        int maxAttempts = 4,
        string? traceId = null)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        Id = id;
        TenantId = tenantId;
        ContentItemId = contentItemId;
        SourceVersionId = sourceVersionId;
        PreviousSourceVersionId = previousSourceVersionId;
        TargetLocale = targetLocale;
        Status = JobStatus.Queued;
        IdempotencyKey = idempotencyKey;
        Priority = priority;
        AttemptCount = 0;
        MaxAttempts = maxAttempts;
        AvailableAt = createdAt;
        TraceId = traceId;
        CreatedAt = createdAt;
    }

    public Result TransitionTo(JobStatus nextStatus, DateTimeOffset now, string? errorCode = null)
    {
        if (Status == JobStatus.Rejected && nextStatus is JobStatus.Approved or JobStatus.CmsDraftCreated)
        {
            return Result.Failure("Rejected jobs can never be published or approved.", ErrorCodes.InvalidState);
        }

        if (Status == JobStatus.Failed && nextStatus is JobStatus.Queued or JobStatus.RetryWait or JobStatus.Translating)
        {
            return Result.Failure("FAILED jobs are not automatically retryable.", ErrorCodes.InvalidState);
        }

        Status = nextStatus;
        if (errorCode is not null)
        {
            LastErrorCode = errorCode;
        }

        if (nextStatus is JobStatus.Rejected or JobStatus.CmsDraftCreated or JobStatus.Failed or JobStatus.DeadLetter)
        {
            CompletedAt = now;
            ClearLease();
        }
        else if (nextStatus is JobStatus.AwaitingReview or JobStatus.NeedsManualReview or JobStatus.Approved)
        {
            ClearLease();
        }

        return Result.Success();
    }

    public void ClaimLease(string workerId, DateTimeOffset now, TimeSpan leaseDuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        LockedBy = workerId;
        LockedAt = now;
        LeaseExpiresAt = now.Add(leaseDuration);
        AttemptCount += 1;
        Status = Status == JobStatus.StaleConflict ? JobStatus.Diffing : JobStatus.Snapshotting;
    }

    public void ScheduleRetryOrDeadLetter(string errorCode, DateTimeOffset now, bool isRetryable)
    {
        LastErrorCode = errorCode;
        ClearLease();

        if (!isRetryable)
        {
            Status = JobStatus.Failed;
            CompletedAt = now;
            return;
        }

        if (AttemptCount >= MaxAttempts)
        {
            Status = JobStatus.DeadLetter;
            CompletedAt = now;
            return;
        }

        int delayIdx = Math.Clamp(AttemptCount - 1, 0, RetryDelays.Length - 1);
        TimeSpan delay = RetryDelays[delayIdx];
        Status = JobStatus.RetryWait;
        AvailableAt = now.Add(delay);
    }

    public void UpdateSourceVersionForReDiff(ContentVersionId newSourceVersionId, ContentVersionId? previousVersionId, DateTimeOffset now)
    {
        PreviousSourceVersionId = previousVersionId;
        SourceVersionId = newSourceVersionId;
        Status = JobStatus.Diffing;
        AvailableAt = now;
        ClearLease();
    }

    public void ReplaceBlocks(IEnumerable<LocalizationJobBlock> blocks)
    {
        _blocks.Clear();
        _blocks.AddRange(blocks);
    }

    private void ClearLease()
    {
        LockedBy = null;
        LockedAt = null;
        LeaseExpiresAt = null;
    }
}

public sealed class LocalizationJobBlock
{
    public JobBlockId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public JobId JobId { get; private set; }
    public BlockKey BlockKey { get; private set; } = null!;
    public string FieldPath { get; private set; } = string.Empty;
    public string BlockType { get; private set; } = string.Empty;
    public string SourceText { get; private set; } = string.Empty;
    public ContentHash SourceHash { get; private set; } = null!;
    public BlockDiffAction ActionType { get; private set; }
    public BlockResolutionAction ResolutionAction { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? PreviousTranslatedText { get; private set; }
    public string? TranslatedText { get; private set; }
    public TranslationMemoryId? TranslationMemoryId { get; private set; }
    public bool HasHumanEditConflict { get; private set; }
    /// <summary>Serialized JSONB validation breakdown.</summary>
    public string? ValidationResult { get; private set; }

    private LocalizationJobBlock() { }

    public LocalizationJobBlock(
        JobBlockId id,
        TenantId tenantId,
        JobId jobId,
        BlockKey blockKey,
        string fieldPath,
        string blockType,
        string sourceText,
        ContentHash sourceHash,
        BlockDiffAction actionType,
        BlockResolutionAction resolutionAction,
        string status,
        string? previousTranslatedText = null,
        string? translatedText = null,
        TranslationMemoryId? translationMemoryId = null,
        bool hasHumanEditConflict = false,
        string? validationResult = null)
    {
        Id = id;
        TenantId = tenantId;
        JobId = jobId;
        BlockKey = blockKey;
        FieldPath = fieldPath;
        BlockType = blockType;
        SourceText = sourceText ?? string.Empty;
        SourceHash = sourceHash;
        ActionType = actionType;
        ResolutionAction = resolutionAction;
        Status = status;
        PreviousTranslatedText = previousTranslatedText;
        TranslatedText = translatedText;
        TranslationMemoryId = translationMemoryId;
        HasHumanEditConflict = hasHumanEditConflict;
        ValidationResult = validationResult;
    }

    public void ApplyTranslationResult(
        string? translatedText,
        string status,
        string validationResultJson,
        BlockResolutionAction? resolutionOverride = null)
    {
        TranslatedText = translatedText;
        Status = status;
        ValidationResult = validationResultJson;
        if (resolutionOverride.HasValue)
        {
            ResolutionAction = resolutionOverride.Value;
        }
    }

    public void ResolveByReviewer(string approvedText)
    {
        TranslatedText = approvedText;
        HasHumanEditConflict = false;
        Status = "APPROVED";
    }
}
