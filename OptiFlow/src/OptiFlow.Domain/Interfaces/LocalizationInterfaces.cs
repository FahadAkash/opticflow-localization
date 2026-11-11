using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Domain.Interfaces;

public sealed record TranslationMemoryKey(
    TenantId TenantId,
    string SourceLocale,
    string TargetLocale,
    ContentHash SourceHash,
    string GlossaryVersion,
    string ContentType);

public interface ITranslationMemory
{
    Task<TranslationMemoryEntry?> FindApprovedAsync(
        TranslationMemoryKey key,
        CancellationToken cancellationToken);

    Task SaveApprovedAsync(
        TranslationMemoryEntry entry,
        CancellationToken cancellationToken);

    Task<TranslationMemoryEntry> SavePendingAiCandidateAsync(
        TranslationMemoryEntry entry,
        CancellationToken cancellationToken);
}

public sealed record GlossaryPair(string Term, string PreferredTranslation, string? ForbiddenTranslation = null);

public sealed record TranslationRequest(
    string SourceLocale,
    string TargetLocale,
    string Brand,
    string ContentType,
    string SourceText,
    IReadOnlyList<GlossaryPair> Glossary);

public sealed record TranslationResult(
    bool IsSuccess,
    string? RawJson,
    string? TranslatedText,
    double? Confidence,
    int InputTokens,
    int OutputTokens,
    int LatencyMs,
    string ModelName,
    string Provider,
    AiInvocationStatus Status,
    bool IsTransientError,
    string? ErrorCode = null);

public interface ILanguageModelClient
{
    Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken);

    Task<bool> WarmUpAsync(CancellationToken cancellationToken);
}

public sealed record DeterministicCheckResult(
    string CheckName,
    bool Passed,
    string? Detail = null);

public sealed record ValidationBreakdown(
    bool IsValid,
    IReadOnlyList<DeterministicCheckResult> Checks,
    bool SemanticCheckExecuted,
    double? SemanticScore = null,
    string? FailureSummary = null);

public interface ITranslationValidator
{
    Task<ValidationBreakdown> ValidateAsync(
        string sourceText,
        TranslationResult modelResult,
        IReadOnlyList<GlossaryPair> glossary,
        ContentItemId contentItemId,
        ContentVersionId expectedSourceVersionId,
        CancellationToken cancellationToken);
}

public sealed record ExistingTranslationState(
    string BlockKey,
    string TranslatedText,
    ContentHash SourceHash,
    TranslationProvenance Provenance);

public sealed record MergeBlockDecision(
    string BlockKey,
    string FieldPath,
    string BlockType,
    string SourceText,
    ContentHash SourceHash,
    BlockDiffAction DiffAction,
    BlockResolutionAction ResolutionAction,
    string? ExistingTranslatedText,
    TranslationMemoryId? MatchedMemoryId,
    string? ReusedTranslationText,
    bool RequiresAiTranslation,
    bool HasHumanEditConflict);

public interface IMergeEngine
{
    Task<IReadOnlyList<MergeBlockDecision>> PlanMergeAsync(
        TenantId tenantId,
        ContentItem contentItem,
        LocaleCode targetLocale,
        ContentDiffResult diffResult,
        IReadOnlyDictionary<string, ExistingTranslationState> existingTranslations,
        string glossaryVersion,
        CancellationToken cancellationToken);
}

public sealed record AnalysisBlockSummary(
    string BlockKey,
    string FieldPath,
    BlockDiffAction DiffAction,
    BlockResolutionAction ResolutionAction,
    bool RequiresAi);

public sealed record AnalysisResult(
    ContentItemId ContentItemId,
    ContentVersionId TargetVersionId,
    int TotalBlocks,
    int UnchangedBlocks,
    int TranslationMemoryHits,
    int BlocksRequiringAi,
    int HumanConflicts,
    IReadOnlyList<AnalysisBlockSummary> Blocks);

public interface ILocalizationOrchestrator
{
    Task<Result<LocalizationJob>> CreateJobAsync(
        TenantId tenantId,
        ContentItemId contentItemId,
        ContentVersionId sourceVersionId,
        string targetLocale,
        string idempotencyKey,
        int priority,
        CancellationToken cancellationToken);

    Task<Result> ProcessAsync(
        Guid localizationJobId,
        CancellationToken cancellationToken);

    Task<Result<AnalysisResult>> AnalyzeAsync(
        Guid contentItemId,
        string targetLocale,
        CancellationToken cancellationToken);
}
