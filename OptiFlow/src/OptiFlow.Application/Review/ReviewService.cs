using System.Text.Json;
using Microsoft.Extensions.Logging;
using OptiFlow.Application.Abstractions;
using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Application.Review;

public sealed class ReviewService : IReviewService
{
    private const string DefaultGlossaryVersion = "v1";

    private readonly IOptiFlowRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ITranslationMemory _translationMemory;
    private readonly IContentSourceResolver _contentSourceResolver;
    private readonly IContentSyncService _contentSyncService;
    private readonly IJobNotificationPublisher _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        IOptiFlowRepository repository,
        ITenantContext tenantContext,
        ITranslationMemory translationMemory,
        IContentSourceResolver contentSourceResolver,
        IContentSyncService contentSyncService,
        IJobNotificationPublisher notifications,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ReviewService> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _translationMemory = translationMemory;
        _contentSourceResolver = contentSourceResolver;
        _contentSyncService = contentSyncService;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReviewTaskResponse>> ListPendingAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return [];
        }

        TenantId tenantId = _tenantContext.CurrentTenantId;
        IReadOnlyList<ReviewTask> tasks = await _repository
            .ListPendingReviewsAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        var responses = new List<ReviewTaskResponse>(tasks.Count);
        foreach (ReviewTask task in tasks)
        {
            LocalizationJob? job = await _repository.GetJobAsync(tenantId, task.JobId, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                continue;
            }

            responses.Add(new ReviewTaskResponse(
                task.Id.Value,
                job.Id.Value,
                job.ContentItemId.Value,
                job.TargetLocale.Value,
                task.Status,
                job.Status,
                task.ExpectedCmsVersion,
                task.CmsDraftId,
                task.CreatedAt,
                task.ReviewedAt));
        }

        return responses;
    }

    public async Task<Result<ReviewDecisionResponse>> ApproveAsync(
        Guid reviewId,
        ApproveReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Result<ReviewDecisionResponse>.Failure("Tenant context is missing.", ErrorCodes.PermissionDenied);
        }

        TenantId tenantId = _tenantContext.CurrentTenantId;
        ReviewTask? task = await _repository
            .GetReviewTaskAsync(tenantId, new ReviewTaskId(reviewId), cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            return Result<ReviewDecisionResponse>.Failure("Review task not found.", ErrorCodes.NotFound);
        }

        LocalizationJob? job = await _repository.GetJobAsync(tenantId, task.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result<ReviewDecisionResponse>.Failure("Localization job not found.", ErrorCodes.NotFound);
        }

        if (job.Status == JobStatus.Rejected)
        {
            return Result<ReviewDecisionResponse>.Failure("Rejected jobs can never be published.", ErrorCodes.InvalidState);
        }

        if (job.Status is not (JobStatus.AwaitingReview or JobStatus.NeedsManualReview))
        {
            return Result<ReviewDecisionResponse>.Failure(
                $"Job status {job.Status} is not ready for approval.",
                ErrorCodes.InvalidState);
        }

        // Prevent draft creation if any block still failed deterministic validation and wasn't overridden by reviewer
        Dictionary<string, string> overrides = (request.BlockOverrides ?? [])
            .GroupBy(o => o.BlockKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().ApprovedText, StringComparer.Ordinal);

        foreach (LocalizationJobBlock block in job.Blocks)
        {
            if (overrides.TryGetValue(block.BlockKey.Value, out string? reviewerText))
            {
                block.ResolveByReviewer(reviewerText);
            }
            else if (string.Equals(block.Status, "VALIDATION_FAILED", StringComparison.Ordinal) ||
                     block.HasHumanEditConflict)
            {
                return Result<ReviewDecisionResponse>.Failure(
                    "All validation failures and human-edit conflicts must be resolved by the reviewer before approval.",
                    ErrorCodes.ValidationFailed);
            }
        }

        ContentItem? item = await _repository
            .GetContentItemAsync(tenantId, job.ContentItemId, cancellationToken)
            .ConfigureAwait(false);

        ContentVersion? sourceVersion = await _repository
            .GetContentVersionAsync(tenantId, job.SourceVersionId, cancellationToken)
            .ConfigureAwait(false);

        if (item is null || sourceVersion is null)
        {
            return Result<ReviewDecisionResponse>.Failure("Content item or version missing.", ErrorCodes.NotFound);
        }

        CmsConnection? connection = await _repository
            .GetConnectionAsync(tenantId, item.ConnectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null)
        {
            return Result<ReviewDecisionResponse>.Failure("CMS connection not found.", ErrorCodes.NotFound);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        job.TransitionTo(JobStatus.Approved, now);

        // Save approved translations + HUMAN_APPROVED translation memory entries
        IReadOnlyDictionary<string, Translation> existingTranslations = await _repository
            .GetTranslationsAsync(tenantId, item.Id, job.TargetLocale, cancellationToken)
            .ConfigureAwait(false);

        var draftBlocks = new List<DraftBlockContent>();

        foreach (LocalizationJobBlock block in job.Blocks)
        {
            if (block.ActionType == BlockDiffAction.Deleted)
            {
                draftBlocks.Add(new DraftBlockContent(block.BlockKey.Value, block.FieldPath, string.Empty, IsRemoval: true));
                continue;
            }

            if (block.TranslatedText is null)
            {
                continue;
            }

            draftBlocks.Add(new DraftBlockContent(block.BlockKey.Value, block.FieldPath, block.TranslatedText));

            bool wasOverridden = overrides.ContainsKey(block.BlockKey.Value);
            TranslationProvenance provenance = wasOverridden
                ? TranslationProvenance.HumanEdited
                : TranslationProvenance.HumanApproved;

            if (existingTranslations.TryGetValue(block.BlockKey.Value, out Translation? existingTr))
            {
                // Preserve HumanEdited provenance if unchanged block was already HumanEdited
                TranslationProvenance effectiveProvenance =
                    block.ResolutionAction == BlockResolutionAction.PreservedHumanEdit
                        ? TranslationProvenance.HumanEdited
                        : provenance;

                existingTr.UpdateTranslation(block.SourceHash, block.TranslatedText, effectiveProvenance, now);
            }
            else
            {
                await _repository.UpsertTranslationAsync(
                    new Translation(
                        TranslationId.New(),
                        tenantId,
                        item.Id,
                        block.BlockKey,
                        job.TargetLocale,
                        block.SourceHash,
                        block.TranslatedText,
                        provenance,
                        now),
                    cancellationToken).ConfigureAwait(false);
            }

            await _translationMemory.SaveApprovedAsync(
                TranslationMemoryEntry.CreateApproved(
                    TranslationMemoryId.New(),
                    tenantId,
                    item.SourceLocale,
                    job.TargetLocale,
                    block.SourceHash,
                    block.TranslatedText,
                    DefaultGlossaryVersion,
                    item.ContentType,
                    _tenantContext.CurrentActorId,
                    now),
                cancellationToken).ConfigureAwait(false);
        }

        string expectedVersion = task.ExpectedCmsVersion ?? sourceVersion.ExternalVersionKey;
        IContentSource cmsSource = _contentSourceResolver.Resolve(connection.Provider);

        DraftResult draftResult = await cmsSource
            .CreateDraftAsync(
                new DraftContent(item.ExternalContentKey.Value, job.TargetLocale.Value, draftBlocks),
                expectedVersion,
                cancellationToken)
            .ConfigureAwait(false);

        if (draftResult.IsVersionMismatch)
        {
            // ETag/version mismatch -> STALE_CONFLICT, re-fetch latest CMS version, restart from DIFFING
            job.TransitionTo(JobStatus.StaleConflict, now, ErrorCodes.StaleConflict);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _notifications.PublishJobStatusChangedAsync(tenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);

            Result<SyncContentResponse> resync = await _contentSyncService
                .SyncAsync(new SyncContentRequest(connection.Id.Value, item.ExternalContentKey.Value, item.SourceLocale.Value), cancellationToken)
                .ConfigureAwait(false);

            if (resync.IsSuccess && resync.Value is not null)
            {
                job.UpdateSourceVersionForReDiff(
                    new ContentVersionId(resync.Value.VersionId),
                    job.SourceVersionId,
                    _timeProvider.GetUtcNow());
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogWarning(
                "Stale CMS conflict on job {JobId} for tenant {TenantId}; re-queued for diff",
                job.Id.Value,
                tenantId.Value);

            return Result<ReviewDecisionResponse>.Failure(
                "CMS source version changed since job started (STALE_CONFLICT). Latest version synced for re-diff.",
                ErrorCodes.StaleConflict);
        }

        if (!draftResult.Succeeded)
        {
            return Result<ReviewDecisionResponse>.Failure(
                draftResult.ErrorMessage ?? "Failed to create CMS draft.",
                ErrorCodes.TransientFailure);
        }

        task.Approve(_tenantContext.CurrentActorId, request.Comments, draftResult.DraftId, now);
        job.TransitionTo(JobStatus.CmsDraftCreated, now);

        await _repository.AddAuditEventAsync(
            new AuditEvent(
                AuditEventId.New(),
                tenantId,
                _tenantContext.CurrentActorId,
                "CMS_DRAFT_CREATED",
                "LocalizationJob",
                job.Id.Value.ToString(),
                JsonSerializer.Serialize(new { draftId = draftResult.DraftId, reviewId = task.Id.Value }),
                now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifications.PublishJobStatusChangedAsync(tenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Approved review {ReviewId} and created CMS draft {DraftId} for job {JobId}",
            task.Id.Value,
            draftResult.DraftId,
            job.Id.Value);

        return Result<ReviewDecisionResponse>.Success(new ReviewDecisionResponse(
            task.Id.Value,
            job.Id.Value,
            job.Status,
            draftResult.DraftId,
            draftResult.NewVersionKey));
    }

    public async Task<Result<ReviewDecisionResponse>> RejectAsync(
        Guid reviewId,
        RejectReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Result<ReviewDecisionResponse>.Failure("Tenant context is missing.", ErrorCodes.PermissionDenied);
        }

        TenantId tenantId = _tenantContext.CurrentTenantId;
        ReviewTask? task = await _repository
            .GetReviewTaskAsync(tenantId, new ReviewTaskId(reviewId), cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            return Result<ReviewDecisionResponse>.Failure("Review task not found.", ErrorCodes.NotFound);
        }

        LocalizationJob? job = await _repository.GetJobAsync(tenantId, task.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result<ReviewDecisionResponse>.Failure("Localization job not found.", ErrorCodes.NotFound);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        task.Reject(_tenantContext.CurrentActorId, request.Comments, now);
        job.TransitionTo(JobStatus.Rejected, now);

        await _repository.AddAuditEventAsync(
            new AuditEvent(
                AuditEventId.New(),
                tenantId,
                _tenantContext.CurrentActorId,
                "REVIEW_REJECTED",
                "LocalizationJob",
                job.Id.Value.ToString(),
                JsonSerializer.Serialize(new { reviewId = task.Id.Value }),
                now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifications.PublishJobStatusChangedAsync(tenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);

        return Result<ReviewDecisionResponse>.Success(new ReviewDecisionResponse(
            task.Id.Value,
            job.Id.Value,
            job.Status,
            CmsDraftId: null,
            NewCmsVersion: null));
    }
}
