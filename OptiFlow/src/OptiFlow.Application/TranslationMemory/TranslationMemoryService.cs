using Microsoft.Extensions.Logging;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Application.TranslationMemory;

public sealed class TranslationMemoryService : ITranslationMemory
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly IOptiFlowRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<TranslationMemoryService> _logger;

    public TranslationMemoryService(
        IOptiFlowRepository repository,
        ICacheService cache,
        ILogger<TranslationMemoryService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TranslationMemoryEntry?> FindApprovedAsync(
        TranslationMemoryKey key,
        CancellationToken cancellationToken)
    {
        string cacheKey = BuildCacheKey(key);
        CachedTmRecord? cached = await _cache
            .GetAsync<CachedTmRecord>(key.TenantId, cacheKey, cancellationToken)
            .ConfigureAwait(false);

        if (cached is not null && cached.ApprovalStatus == ApprovalStatus.Approved)
        {
            return TranslationMemoryEntry.CreateApproved(
                new TranslationMemoryId(cached.Id),
                key.TenantId,
                new LocaleCode(key.SourceLocale),
                new LocaleCode(key.TargetLocale),
                key.SourceHash,
                cached.TranslatedText,
                key.GlossaryVersion,
                key.ContentType,
                cached.ApprovedBy,
                cached.ApprovedAt);
        }

        TranslationMemoryEntry? entry = await _repository
            .FindApprovedTranslationMemoryAsync(key, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null || entry.ApprovalStatus != ApprovalStatus.Approved)
        {
            return null;
        }

        await _cache.SetAsync(
            key.TenantId,
            cacheKey,
            new CachedTmRecord(
                entry.Id.Value,
                entry.TranslatedText,
                entry.ApprovalStatus,
                entry.ApprovedBy ?? "reviewer",
                entry.ApprovedAt ?? entry.CreatedAt),
            CacheTtl,
            cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "TM approved hit {TmId} for tenant {TenantId} hash {SourceHash}",
            entry.Id.Value,
            key.TenantId.Value,
            key.SourceHash.Value);

        return entry;
    }

    public async Task SaveApprovedAsync(
        TranslationMemoryEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry.ApprovalStatus != ApprovalStatus.Approved ||
            entry.Provenance != TranslationProvenance.HumanApproved)
        {
            throw new InvalidOperationException("Only HUMAN_APPROVED entries can be saved as approved TM entries.");
        }

        var key = new TranslationMemoryKey(
            entry.TenantId,
            entry.SourceLocale.Value,
            entry.TargetLocale.Value,
            entry.SourceHash,
            entry.GlossaryVersion,
            entry.ContentType);

        TranslationMemoryEntry? existing = await _repository
            .FindTranslationMemoryByCompositeKeyAsync(key, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await _repository.AddTranslationMemoryEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.Approve(
                entry.TranslatedText,
                entry.ApprovedBy ?? "reviewer",
                entry.ApprovedAt ?? entry.CreatedAt);
        }

        await _cache.SetAsync(
            entry.TenantId,
            BuildCacheKey(key),
            new CachedTmRecord(
                (existing ?? entry).Id.Value,
                entry.TranslatedText,
                ApprovalStatus.Approved,
                entry.ApprovedBy ?? "reviewer",
                entry.ApprovedAt ?? entry.CreatedAt),
            CacheTtl,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<TranslationMemoryEntry> SavePendingAiCandidateAsync(
        TranslationMemoryEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry.ApprovalStatus != ApprovalStatus.Pending ||
            entry.Provenance != TranslationProvenance.AiGenerated)
        {
            throw new InvalidOperationException("AI candidates must have Pending approval status and AiGenerated provenance.");
        }

        var key = new TranslationMemoryKey(
            entry.TenantId,
            entry.SourceLocale.Value,
            entry.TargetLocale.Value,
            entry.SourceHash,
            entry.GlossaryVersion,
            entry.ContentType);

        TranslationMemoryEntry? existing = await _repository
            .FindTranslationMemoryByCompositeKeyAsync(key, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        await _repository.AddTranslationMemoryEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    private static string BuildCacheKey(TranslationMemoryKey key) =>
        $"tm:{key.SourceLocale}:{key.TargetLocale}:{key.SourceHash.Value}:{key.GlossaryVersion}:{key.ContentType}";

    private sealed record CachedTmRecord(
        Guid Id,
        string TranslatedText,
        ApprovalStatus ApprovalStatus,
        string ApprovedBy,
        DateTimeOffset ApprovedAt);
}
