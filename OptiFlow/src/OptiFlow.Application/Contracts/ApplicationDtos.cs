using OptiFlow.Domain.Common;
using OptiFlow.Domain.Enums;

namespace OptiFlow.Application.Contracts;

public sealed record CreateConnectionRequest(
    CmsProvider Provider,
    string BaseUrl,
    string ApiSecret);

public sealed record ConnectionResponse(
    Guid Id,
    CmsProvider Provider,
    string BaseUrl,
    CmsConnectionStatus Status,
    DateTimeOffset CreatedAt);

public sealed record SyncContentRequest(
    Guid ConnectionId,
    string ExternalContentKey,
    string? Locale = null);

public sealed record SyncContentResponse(
    Guid ContentItemId,
    Guid VersionId,
    string ExternalContentKey,
    string ExternalVersionKey,
    int VersionNumber,
    string ContentHash,
    int BlockCount,
    bool IsNewVersion);

public sealed record ContentVersionResponse(
    Guid Id,
    string ExternalVersionKey,
    string Locale,
    int VersionNumber,
    string ContentHash,
    int BlockCount,
    DateTimeOffset CapturedAt);

public sealed record DiffBlockDto(
    string BlockKey,
    string FieldPath,
    string BlockType,
    string ContentHash,
    BlockDiffAction Action,
    int Ordinal);

public sealed record DiffResponse(
    Guid ContentItemId,
    Guid? PreviousVersionId,
    Guid CurrentVersionId,
    int TotalBlocks,
    int UnchangedCount,
    int ModifiedCount,
    int AddedCount,
    int DeletedCount,
    IReadOnlyList<DiffBlockDto> Blocks);

public sealed record CreateJobRequest(
    Guid ContentId,
    Guid SourceVersionId,
    IReadOnlyList<string> TargetLocales,
    string? IdempotencyKey = null,
    int Priority = 0);

public sealed record CreateJobResponse(
    IReadOnlyList<JobStatusResponse> Jobs);

public sealed record JobStatusResponse(
    Guid JobId,
    Guid ContentItemId,
    Guid SourceVersionId,
    string TargetLocale,
    JobStatus Status,
    int AttemptCount,
    int MaxAttempts,
    string? LastErrorCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record JobBlockResponse(
    Guid Id,
    string BlockKey,
    string FieldPath,
    string BlockType,
    string SourceText,
    string SourceHash,
    BlockDiffAction ActionType,
    BlockResolutionAction ResolutionAction,
    string Status,
    string? PreviousTranslatedText,
    string? TranslatedText,
    Guid? TranslationMemoryId,
    bool HasHumanEditConflict,
    string? ValidationResult);

public sealed record ReviewTaskResponse(
    Guid ReviewId,
    Guid JobId,
    Guid ContentItemId,
    string TargetLocale,
    ReviewStatus Status,
    JobStatus JobStatus,
    string? ExpectedCmsVersion,
    string? CmsDraftId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record ReviewBlockOverride(
    string BlockKey,
    string ApprovedText);

public sealed record ApproveReviewRequest(
    string? Comments = null,
    IReadOnlyList<ReviewBlockOverride>? BlockOverrides = null);

public sealed record RejectReviewRequest(
    string? Comments = null);

public sealed record ReviewDecisionResponse(
    Guid ReviewId,
    Guid JobId,
    JobStatus JobStatus,
    string? CmsDraftId,
    string? NewCmsVersion);

public sealed record CostAnalyticsResponse(
    int TotalJobs,
    int TotalBlocksProcessed,
    int BlocksReusedOrPreserved,
    double TranslationReuseRate,
    double ContentPreservationRate,
    long IncrementalInputTokens,
    long IncrementalOutputTokens,
    long EstimatedBaselineTokens,
    double AiCreditReductionPercent,
    decimal EstimatedLocalCostUsd,
    decimal EstimatedCloudEquivalentCostUsd);

public sealed record PerformanceAnalyticsResponse(
    int TotalJobs,
    int SucceededJobs,
    int FailedOrDeadLetterJobs,
    double JobSuccessRate,
    double LatencyP50Ms,
    double LatencyP95Ms,
    double LatencyP99Ms);

public sealed record OptimizelyWebhookRequest(
    string Provider,
    string ExternalEventId,
    Guid ConnectionId,
    string ExternalContentKey,
    string? TargetLocale = null,
    string? SourceLocale = null);

public sealed record WebhookIngestResponse(
    Guid WebhookId,
    bool Deduplicated,
    Guid? CreatedJobId);

public sealed record ApiError(string Message, string Code)
{
    public static ApiError NotFound(string resource) => new($"{resource} not found.", ErrorCodes.NotFound);
    public static ApiError Conflict(string message, string code = ErrorCodes.Duplicate) => new(message, code);
    public static ApiError BadRequest(string message, string code = ErrorCodes.ValidationFailed) => new(message, code);
    public static ApiError Internal() => new("An internal error occurred.", "INTERNAL_ERROR");
}
