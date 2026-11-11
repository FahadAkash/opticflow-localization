using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OptiFlow.Application.Diff;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Application.Localization;

public sealed class LocalizationOrchestrator : ILocalizationOrchestrator
{
    private static readonly ActivitySource TelemetrySource = new("OptiFlow.Application");
    private const string DefaultGlossaryVersion = "v1";
    private const string PromptVersion = "gemma4b-json-v1";

    private readonly IOptiFlowRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IContentDiffEngine _diffEngine;
    private readonly IMergeEngine _mergeEngine;
    private readonly ITranslationMemory _translationMemory;
    private readonly ILanguageModelClient _languageModelClient;
    private readonly ITranslationValidator _validator;
    private readonly IJobNotificationPublisher _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LocalizationOrchestrator> _logger;

    public LocalizationOrchestrator(
        IOptiFlowRepository repository,
        ITenantContext tenantContext,
        IContentDiffEngine diffEngine,
        IMergeEngine mergeEngine,
        ITranslationMemory translationMemory,
        ILanguageModelClient languageModelClient,
        ITranslationValidator validator,
        IJobNotificationPublisher notifications,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<LocalizationOrchestrator> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _diffEngine = diffEngine;
        _mergeEngine = mergeEngine;
        _translationMemory = translationMemory;
        _languageModelClient = languageModelClient;
        _validator = validator;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<LocalizationJob>> CreateJobAsync(
        TenantId tenantId,
        ContentItemId contentItemId,
        ContentVersionId sourceVersionId,
        string targetLocale,
        string idempotencyKey,
        int priority,
        CancellationToken cancellationToken)
    {
        var key = new IdempotencyKey(idempotencyKey);
        LocalizationJob? existing = await _repository
            .GetJobByIdempotencyKeyAsync(tenantId, key, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Idempotent job hit {JobId} for tenant {TenantId}",
                existing.Id.Value,
                tenantId.Value);
            return Result<LocalizationJob>.Success(existing);
        }

        ContentItem? item = await _repository
            .GetContentItemAsync(tenantId, contentItemId, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return Result<LocalizationJob>.Failure("Content item not found.", ErrorCodes.NotFound);
        }

        ContentVersion? sourceVersion = await _repository
            .GetContentVersionAsync(tenantId, sourceVersionId, cancellationToken)
            .ConfigureAwait(false);

        if (sourceVersion is null || sourceVersion.ContentItemId != contentItemId)
        {
            return Result<LocalizationJob>.Failure("Source content version not found.", ErrorCodes.NotFound);
        }

        IReadOnlyList<ContentVersion> allVersions = await _repository
            .ListContentVersionsAsync(tenantId, contentItemId, cancellationToken)
            .ConfigureAwait(false);

        ContentVersion? previousVersion = allVersions
            .Where(v => v.VersionNumber < sourceVersion.VersionNumber &&
                        string.Equals(v.Locale.Value, sourceVersion.Locale.Value, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        DateTimeOffset now = _timeProvider.GetUtcNow();
        string? traceId = Activity.Current?.TraceId.ToString();

        var job = new LocalizationJob(
            JobId.New(),
            tenantId,
            contentItemId,
            sourceVersionId,
            previousVersion?.Id,
            new LocaleCode(targetLocale),
            key,
            now,
            priority,
            maxAttempts: 4,
            traceId: traceId);

        await _repository.AddJobAsync(job, cancellationToken).ConfigureAwait(false);
        await _repository.AddOutboxMessageAsync(
            new OutboxMessage(
                OutboxMessageId.New(),
                tenantId,
                job.Id.Value,
                "LOCALIZATION_JOB_QUEUED",
                JsonSerializer.Serialize(new { jobId = job.Id.Value, targetLocale }),
                now),
            cancellationToken).ConfigureAwait(false);

        await _repository.AddAuditEventAsync(
            new AuditEvent(
                AuditEventId.New(),
                tenantId,
                _tenantContext.HasTenant ? _tenantContext.CurrentActorId : "system",
                "LOCALIZATION_JOB_CREATED",
                "LocalizationJob",
                job.Id.Value.ToString(),
                JsonSerializer.Serialize(new { contentItemId = contentItemId.Value, targetLocale }),
                now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifications.PublishJobStatusChangedAsync(tenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Queued localization job {JobId} for tenant {TenantId} item {ContentItemId} locale {TargetLocale}",
            job.Id.Value,
            tenantId.Value,
            contentItemId.Value,
            targetLocale);

        return Result<LocalizationJob>.Success(job);
    }

    public async Task<Result<AnalysisResult>> AnalyzeAsync(
        Guid contentItemId,
        string targetLocale,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Result<AnalysisResult>.Failure("Tenant context is missing.", ErrorCodes.PermissionDenied);
        }

        TenantId tenantId = _tenantContext.CurrentTenantId;
        var itemId = new ContentItemId(contentItemId);
        ContentItem? item = await _repository.GetContentItemAsync(tenantId, itemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return Result<AnalysisResult>.Failure("Content item not found.", ErrorCodes.NotFound);
        }

        IReadOnlyList<ContentVersion> versions = await _repository
            .ListContentVersionsAsync(tenantId, itemId, cancellationToken)
            .ConfigureAwait(false);

        List<ContentVersion> ordered = versions.OrderBy(v => v.VersionNumber).ToList();
        if (ordered.Count == 0)
        {
            return Result<AnalysisResult>.Failure("No snapshots found for content item.", ErrorCodes.NotFound);
        }

        ContentVersion latest = ordered[^1];
        ContentVersion? previous = ordered.Count > 1 ? ordered[^2] : null;

        ContentDiffResult diff = _diffEngine.Compare(
            previous is null ? null : ContentDiffEngine.ToSnapshot(item, previous),
            ContentDiffEngine.ToSnapshot(item, latest));

        IReadOnlyDictionary<string, Translation> translations = await _repository
            .GetTranslationsAsync(tenantId, itemId, new LocaleCode(targetLocale), cancellationToken)
            .ConfigureAwait(false);

        var existingStates = translations.ToDictionary(
            kvp => kvp.Key,
            kvp => new ExistingTranslationState(
                kvp.Value.BlockKey.Value,
                kvp.Value.TranslatedText,
                kvp.Value.SourceHash,
                kvp.Value.Provenance),
            StringComparer.Ordinal);

        IReadOnlyList<MergeBlockDecision> mergePlan = await _mergeEngine.PlanMergeAsync(
            tenantId,
            item,
            new LocaleCode(targetLocale),
            diff,
            existingStates,
            DefaultGlossaryVersion,
            cancellationToken).ConfigureAwait(false);

        return Result<AnalysisResult>.Success(new AnalysisResult(
            item.Id,
            latest.Id,
            mergePlan.Count,
            mergePlan.Count(m => m.ResolutionAction is BlockResolutionAction.ReusedUnchanged or BlockResolutionAction.PreservedHumanEdit),
            mergePlan.Count(m => m.ResolutionAction == BlockResolutionAction.ReusedTranslationMemory),
            mergePlan.Count(m => m.RequiresAiTranslation),
            mergePlan.Count(m => m.HasHumanEditConflict),
            mergePlan.Select(m => new AnalysisBlockSummary(
                m.BlockKey,
                m.FieldPath,
                m.DiffAction,
                m.ResolutionAction,
                m.RequiresAiTranslation)).ToList()));
    }

    public async Task<Result> ProcessAsync(Guid localizationJobId, CancellationToken cancellationToken)
    {
        LocalizationJob? job = await _repository
            .GetJobByIdAsync(new JobId(localizationJobId), cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
        {
            return Result.Failure("Localization job not found.", ErrorCodes.NotFound);
        }

        if (job.Status is JobStatus.Rejected or JobStatus.CmsDraftCreated or JobStatus.Failed or JobStatus.DeadLetter)
        {
            return Result.Failure($"Job {job.Id.Value} is in terminal status {job.Status}.", ErrorCodes.InvalidState);
        }

        _tenantContext.SetTenant(job.TenantId, "optiflow-worker");

        using Activity? activity = TelemetrySource.StartActivity("ProcessLocalizationJob");
        activity?.SetTag("tenant.id", job.TenantId.Value);
        activity?.SetTag("job.id", job.Id.Value);
        activity?.SetTag("job.target_locale", job.TargetLocale.Value);

        try
        {
            await TransitionAndNotifyAsync(job, JobStatus.Snapshotting, cancellationToken).ConfigureAwait(false);

            ContentItem? item = await _repository
                .GetContentItemAsync(job.TenantId, job.ContentItemId, cancellationToken)
                .ConfigureAwait(false);

            ContentVersion? sourceVersion = await _repository
                .GetContentVersionAsync(job.TenantId, job.SourceVersionId, cancellationToken)
                .ConfigureAwait(false);

            if (item is null || sourceVersion is null)
            {
                job.ScheduleRetryOrDeadLetter(ErrorCodes.NotFound, _timeProvider.GetUtcNow(), isRetryable: false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await _notifications.PublishJobStatusChangedAsync(job.TenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);
                return Result.Failure("Missing content item or source snapshot.", ErrorCodes.NotFound);
            }

            await TransitionAndNotifyAsync(job, JobStatus.Diffing, cancellationToken).ConfigureAwait(false);

            ContentVersion? previousVersion = job.PreviousSourceVersionId.HasValue
                ? await _repository.GetContentVersionAsync(job.TenantId, job.PreviousSourceVersionId.Value, cancellationToken).ConfigureAwait(false)
                : null;

            ContentSnapshot? prevSnapshot = previousVersion is null ? null : ContentDiffEngine.ToSnapshot(item, previousVersion);
            ContentSnapshot currSnapshot = ContentDiffEngine.ToSnapshot(item, sourceVersion);
            ContentDiffResult diff = _diffEngine.Compare(prevSnapshot, currSnapshot);

            IReadOnlyDictionary<string, Translation> existingMap = await _repository
                .GetTranslationsAsync(job.TenantId, item.Id, job.TargetLocale, cancellationToken)
                .ConfigureAwait(false);

            var existingStates = existingMap.ToDictionary(
                kvp => kvp.Key,
                kvp => new ExistingTranslationState(
                    kvp.Value.BlockKey.Value,
                    kvp.Value.TranslatedText,
                    kvp.Value.SourceHash,
                    kvp.Value.Provenance),
                StringComparer.Ordinal);

            IReadOnlyList<MergeBlockDecision> mergeDecisions = await _mergeEngine.PlanMergeAsync(
                job.TenantId,
                item,
                job.TargetLocale,
                diff,
                existingStates,
                DefaultGlossaryVersion,
                cancellationToken).ConfigureAwait(false);

            activity?.SetTag("job.blocks.total", mergeDecisions.Count);

            var jobBlocks = new List<LocalizationJobBlock>(mergeDecisions.Count);
            foreach (MergeBlockDecision decision in mergeDecisions)
            {
                string initialStatus = decision.RequiresAiTranslation
                    ? "PENDING_AI"
                    : decision.HasHumanEditConflict
                        ? "CONFLICT"
                        : "REUSED";

                jobBlocks.Add(new LocalizationJobBlock(
                    JobBlockId.New(),
                    job.TenantId,
                    job.Id,
                    new BlockKey(decision.BlockKey),
                    decision.FieldPath,
                    decision.BlockType,
                    decision.SourceText,
                    decision.SourceHash,
                    decision.DiffAction,
                    decision.ResolutionAction,
                    initialStatus,
                    decision.ExistingTranslatedText,
                    decision.ReusedTranslationText,
                    decision.MatchedMemoryId,
                    decision.HasHumanEditConflict));
            }

            job.ReplaceBlocks(jobBlocks);

            List<LocalizationJobBlock> blocksNeedingAi = jobBlocks
                .Where(b => b.ResolutionAction == BlockResolutionAction.AiTranslated)
                .ToList();

            activity?.SetTag("job.blocks.ai", blocksNeedingAi.Count);

            if (blocksNeedingAi.Count == 0)
            {
                await EnsureReviewTaskAndAwaitReviewAsync(job, sourceVersion.ExternalVersionKey, cancellationToken)
                    .ConfigureAwait(false);
                return Result.Success();
            }

            await TransitionAndNotifyAsync(job, JobStatus.Translating, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<GlossaryTerm> glossaryTerms = await _repository
                .GetGlossaryTermsAsync(job.TenantId, item.SourceLocale, job.TargetLocale, DefaultGlossaryVersion, cancellationToken)
                .ConfigureAwait(false);

            List<GlossaryPair> glossaryPairs = glossaryTerms
                .Select(g => new GlossaryPair(g.SourceTerm, g.PreferredTranslation, g.ForbiddenTranslation))
                .ToList();

            bool anyValidationFailed = false;
            bool anyTransientFailure = false;
            string? transientErrorCode = null;
            bool anyPermissionFailure = false;
            string? permissionErrorCode = null;

            foreach (LocalizationJobBlock block in blocksNeedingAi)
            {
                var request = new TranslationRequest(
                    item.SourceLocale.Value,
                    job.TargetLocale.Value,
                    item.ContentType,
                    block.BlockType,
                    block.SourceText,
                    glossaryPairs);

                TranslationResult aiResult = await _languageModelClient
                    .TranslateAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                DateTimeOffset now = _timeProvider.GetUtcNow();
                decimal estimatedCost = EstimateCloudEquivalentCost(aiResult.InputTokens, aiResult.OutputTokens);

                if (!aiResult.IsSuccess)
                {
                    await _repository.AddAiInvocationAsync(
                        new AiInvocation(
                            AiInvocationId.New(),
                            job.TenantId,
                            job.Id,
                            block.Id,
                            aiResult.Provider,
                            aiResult.ModelName,
                            PromptVersion,
                            aiResult.InputTokens,
                            aiResult.OutputTokens,
                            aiResult.LatencyMs,
                            estimatedCost,
                            aiResult.Status,
                            now),
                        cancellationToken).ConfigureAwait(false);

                    if (string.Equals(aiResult.ErrorCode, ErrorCodes.PermissionDenied, StringComparison.Ordinal))
                    {
                        anyPermissionFailure = true;
                        permissionErrorCode = aiResult.ErrorCode;
                        block.ApplyTranslationResult(null, "FAILED", JsonSerializer.Serialize(new { error = aiResult.ErrorCode }));
                        break;
                    }

                    if (aiResult.IsTransientError)
                    {
                        anyTransientFailure = true;
                        transientErrorCode = aiResult.ErrorCode ?? ErrorCodes.TransientFailure;
                        block.ApplyTranslationResult(null, "RETRY_PENDING", JsonSerializer.Serialize(new { error = transientErrorCode }));
                        break;
                    }
                }

                await TransitionAndNotifyAsync(job, JobStatus.Validating, cancellationToken).ConfigureAwait(false);

                ValidationBreakdown validation = await _validator.ValidateAsync(
                    block.SourceText,
                    aiResult,
                    glossaryPairs,
                    item.Id,
                    job.SourceVersionId,
                    cancellationToken).ConfigureAwait(false);

                string validationJson = JsonSerializer.Serialize(validation);

                await _repository.AddAiInvocationAsync(
                    new AiInvocation(
                        AiInvocationId.New(),
                        job.TenantId,
                        job.Id,
                        block.Id,
                        aiResult.Provider,
                        aiResult.ModelName,
                        PromptVersion,
                        aiResult.InputTokens,
                        aiResult.OutputTokens,
                        aiResult.LatencyMs,
                        estimatedCost,
                        validation.IsValid ? AiInvocationStatus.Succeeded : AiInvocationStatus.ValidationFailed,
                        now),
                    cancellationToken).ConfigureAwait(false);

                if (validation.IsValid)
                {
                    block.ApplyTranslationResult(aiResult.TranslatedText, "VALIDATED", validationJson);

                    // AI output is ALWAYS saved as Pending (never auto-approved in TM)
                    var pendingCandidate = TranslationMemoryEntry.CreatePendingAiCandidate(
                        TranslationMemoryId.New(),
                        job.TenantId,
                        item.SourceLocale,
                        job.TargetLocale,
                        block.SourceHash,
                        aiResult.TranslatedText!,
                        DefaultGlossaryVersion,
                        item.ContentType,
                        now);
                    await _translationMemory.SavePendingAiCandidateAsync(pendingCandidate, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    anyValidationFailed = true;
                    block.ApplyTranslationResult(aiResult.TranslatedText, "VALIDATION_FAILED", validationJson);
                }
            }

            if (anyPermissionFailure)
            {
                job.ScheduleRetryOrDeadLetter(permissionErrorCode ?? ErrorCodes.PermissionDenied, _timeProvider.GetUtcNow(), isRetryable: false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await _notifications.PublishJobStatusChangedAsync(job.TenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);
                return Result.Failure("Permission failure during AI translation.", ErrorCodes.PermissionDenied);
            }

            if (anyTransientFailure)
            {
                job.ScheduleRetryOrDeadLetter(transientErrorCode ?? ErrorCodes.TransientFailure, _timeProvider.GetUtcNow(), isRetryable: true);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await _notifications.PublishJobStatusChangedAsync(job.TenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);
                return Result.Failure("Transient AI failure; scheduled retry or dead-letter.", transientErrorCode ?? ErrorCodes.TransientFailure);
            }

            if (anyValidationFailed)
            {
                // Validation errors are NEVER retried blindly and NEVER create a CMS draft
                job.TransitionTo(JobStatus.NeedsManualReview, _timeProvider.GetUtcNow(), ErrorCodes.ValidationFailed);
                await EnsureReviewTaskAsync(job, sourceVersion.ExternalVersionKey, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await _notifications.PublishJobStatusChangedAsync(job.TenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);
                return Result.Success();
            }

            await EnsureReviewTaskAndAwaitReviewAsync(job, sourceVersion.ExternalVersionKey, cancellationToken)
                .ConfigureAwait(false);

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
        {
            _logger.LogWarning(ex, "Transient error processing job {JobId} for tenant {TenantId}", job.Id.Value, job.TenantId.Value);
            job.ScheduleRetryOrDeadLetter(ErrorCodes.TransientFailure, _timeProvider.GetUtcNow(), isRetryable: true);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _notifications.PublishJobStatusChangedAsync(job.TenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);
            return Result.Failure("Transient failure during processing.", ErrorCodes.TransientFailure);
        }
    }

    private async Task TransitionAndNotifyAsync(LocalizationJob job, JobStatus status, CancellationToken cancellationToken)
    {
        job.TransitionTo(status, _timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifications.PublishJobStatusChangedAsync(job.TenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureReviewTaskAndAwaitReviewAsync(
        LocalizationJob job,
        string expectedCmsVersion,
        CancellationToken cancellationToken)
    {
        await EnsureReviewTaskAsync(job, expectedCmsVersion, cancellationToken).ConfigureAwait(false);
        job.TransitionTo(JobStatus.AwaitingReview, _timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifications.PublishJobStatusChangedAsync(job.TenantId, job.Id, job.Status, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureReviewTaskAsync(
        LocalizationJob job,
        string expectedCmsVersion,
        CancellationToken cancellationToken)
    {
        ReviewTask? existingTask = await _repository
            .GetReviewTaskByJobAsync(job.TenantId, job.Id, cancellationToken)
            .ConfigureAwait(false);

        if (existingTask is null)
        {
            await _repository.AddReviewTaskAsync(
                new ReviewTask(
                    ReviewTaskId.New(),
                    job.TenantId,
                    job.Id,
                    expectedCmsVersion,
                    _timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static decimal EstimateCloudEquivalentCost(int inputTokens, int outputTokens)
    {
        // Reference cloud benchmark rate ($1.25 / 1M input, $5.00 / 1M output)
        return Math.Round((inputTokens / 1_000_000m) * 1.25m + (outputTokens / 1_000_000m) * 5.00m, 6);
    }
}
