namespace OptiFlow.Domain.Enums;

public enum TenantStatus
{
    Active = 1,
    Suspended = 2
}

public enum CmsConnectionStatus
{
    Active = 1,
    Disabled = 2
}

public enum CmsProvider
{
    LocalTest = 1,
    OptimizelySaaS = 2,
    OptimizelyCms12 = 3
}

public enum JobStatus
{
    Queued = 1,
    Snapshotting = 2,
    Diffing = 3,
    Translating = 4,
    Validating = 5,
    RetryWait = 6,
    NeedsManualReview = 7,
    AwaitingReview = 8,
    Approved = 9,
    Rejected = 10,
    CmsDraftCreated = 11,
    StaleConflict = 12,
    Failed = 13,
    DeadLetter = 14
}

public enum BlockDiffAction
{
    Unchanged = 1,
    Modified = 2,
    Added = 3,
    Deleted = 4
}

public enum BlockResolutionAction
{
    ReusedUnchanged = 1,
    PreservedHumanEdit = 2,
    ReusedTranslationMemory = 3,
    AiTranslated = 4,
    ConflictRequiresReview = 5,
    ProposedRemoval = 6
}

public enum TranslationProvenance
{
    AiGenerated = 1,
    HumanEdited = 2,
    HumanApproved = 3
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum ReviewStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum WebhookProcessingStatus
{
    Pending = 1,
    Processed = 2,
    Duplicate = 3,
    Failed = 4
}

public enum OutboxStatus
{
    Pending = 1,
    Published = 2,
    Failed = 3
}

public enum AiInvocationStatus
{
    Succeeded = 1,
    ValidationFailed = 2,
    TransientError = 3,
    PermanentError = 4,
    TimedOut = 5
}
