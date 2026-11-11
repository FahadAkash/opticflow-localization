using OptiFlow.Domain.Enums;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Domain.Interfaces;

public sealed record ContentBlockSnapshot(
    string BlockKey,
    string FieldPath,
    string BlockType,
    string SourceText,
    int Ordinal);

public sealed record ContentSnapshot(
    string ExternalContentKey,
    string ExternalVersionKey,
    string Locale,
    int VersionNumber,
    string ContentType,
    string RawPayloadJson,
    IReadOnlyList<ContentBlockSnapshot> Blocks);

public sealed record ContentVersionSummary(
    string ExternalVersionKey,
    int VersionNumber,
    string Locale,
    DateTimeOffset CapturedAt);

public sealed record DraftBlockContent(
    string BlockKey,
    string FieldPath,
    string TranslatedText,
    bool IsRemoval = false);

public sealed record DraftContent(
    string ExternalContentKey,
    string TargetLocale,
    IReadOnlyList<DraftBlockContent> Blocks);

public sealed record DraftResult(
    bool Succeeded,
    bool IsVersionMismatch,
    string? DraftId,
    string? NewVersionKey,
    string? ErrorMessage = null);

public interface IContentSource
{
    Task<ContentSnapshot?> GetContentAsync(
        string contentKey,
        string locale,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentVersionSummary>> GetVersionsAsync(
        string contentKey,
        CancellationToken cancellationToken);

    Task<DraftResult> CreateDraftAsync(
        DraftContent content,
        string expectedVersion,
        CancellationToken cancellationToken);
}

public sealed record DiffBlockResult(
    string BlockKey,
    string FieldPath,
    string BlockType,
    string SourceText,
    ContentHash SourceHash,
    BlockDiffAction Action,
    int Ordinal);

public sealed record ContentDiffResult(
    int TotalBlocks,
    int UnchangedCount,
    int ModifiedCount,
    int AddedCount,
    int DeletedCount,
    IReadOnlyList<DiffBlockResult> Blocks);

public interface IContentDiffEngine
{
    ContentDiffResult Compare(ContentSnapshot? previous, ContentSnapshot current);
}
