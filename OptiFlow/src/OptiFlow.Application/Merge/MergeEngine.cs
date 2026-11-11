using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Application.Merge;

public sealed class MergeEngine : IMergeEngine
{
    private readonly ITranslationMemory _translationMemory;

    public MergeEngine(ITranslationMemory translationMemory)
    {
        _translationMemory = translationMemory;
    }

    public async Task<IReadOnlyList<MergeBlockDecision>> PlanMergeAsync(
        TenantId tenantId,
        ContentItem contentItem,
        LocaleCode targetLocale,
        ContentDiffResult diffResult,
        IReadOnlyDictionary<string, ExistingTranslationState> existingTranslations,
        string glossaryVersion,
        CancellationToken cancellationToken)
    {
        var decisions = new List<MergeBlockDecision>(diffResult.Blocks.Count);

        foreach (DiffBlockResult block in diffResult.Blocks)
        {
            existingTranslations.TryGetValue(block.BlockKey, out ExistingTranslationState? existing);
            bool isHumanEdited = existing is not null &&
                                 existing.Provenance is TranslationProvenance.HumanEdited or TranslationProvenance.HumanApproved;

            switch (block.Action)
            {
                case BlockDiffAction.Deleted:
                    decisions.Add(new MergeBlockDecision(
                        block.BlockKey,
                        block.FieldPath,
                        block.BlockType,
                        block.SourceText,
                        block.SourceHash,
                        BlockDiffAction.Deleted,
                        BlockResolutionAction.ProposedRemoval,
                        existing?.TranslatedText,
                        null,
                        null,
                        RequiresAiTranslation: false,
                        HasHumanEditConflict: false));
                    break;

                case BlockDiffAction.Unchanged:
                    decisions.Add(new MergeBlockDecision(
                        block.BlockKey,
                        block.FieldPath,
                        block.BlockType,
                        block.SourceText,
                        block.SourceHash,
                        BlockDiffAction.Unchanged,
                        isHumanEdited
                            ? BlockResolutionAction.PreservedHumanEdit
                            : BlockResolutionAction.ReusedUnchanged,
                        existing?.TranslatedText,
                        null,
                        existing?.TranslatedText,
                        RequiresAiTranslation: false,
                        HasHumanEditConflict: false));
                    break;

                case BlockDiffAction.Modified when existing?.Provenance == TranslationProvenance.HumanEdited:
                    // Modified + target human-edited => Conflict, send to reviewer (never auto-overwrite)
                    decisions.Add(new MergeBlockDecision(
                        block.BlockKey,
                        block.FieldPath,
                        block.BlockType,
                        block.SourceText,
                        block.SourceHash,
                        BlockDiffAction.Modified,
                        BlockResolutionAction.ConflictRequiresReview,
                        existing.TranslatedText,
                        null,
                        existing.TranslatedText,
                        RequiresAiTranslation: false,
                        HasHumanEditConflict: true));
                    break;

                case BlockDiffAction.Modified:
                case BlockDiffAction.Added:
                {
                    var tmKey = new TranslationMemoryKey(
                        tenantId,
                        contentItem.SourceLocale.Value,
                        targetLocale.Value,
                        block.SourceHash,
                        glossaryVersion,
                        contentItem.ContentType);

                    TranslationMemoryEntry? tmHit = await _translationMemory
                        .FindApprovedAsync(tmKey, cancellationToken)
                        .ConfigureAwait(false);

                    if (tmHit is not null)
                    {
                        decisions.Add(new MergeBlockDecision(
                            block.BlockKey,
                            block.FieldPath,
                            block.BlockType,
                            block.SourceText,
                            block.SourceHash,
                            block.Action,
                            BlockResolutionAction.ReusedTranslationMemory,
                            existing?.TranslatedText,
                            tmHit.Id,
                            tmHit.TranslatedText,
                            RequiresAiTranslation: false,
                            HasHumanEditConflict: false));
                    }
                    else
                    {
                        decisions.Add(new MergeBlockDecision(
                            block.BlockKey,
                            block.FieldPath,
                            block.BlockType,
                            block.SourceText,
                            block.SourceHash,
                            block.Action,
                            BlockResolutionAction.AiTranslated,
                            existing?.TranslatedText,
                            null,
                            null,
                            RequiresAiTranslation: true,
                            HasHumanEditConflict: false));
                    }
                    break;
                }
            }
        }

        return decisions;
    }
}
