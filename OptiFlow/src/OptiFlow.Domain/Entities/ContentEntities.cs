using OptiFlow.Domain.Enums;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Domain.Entities;

public sealed class ContentItem
{
    private readonly List<ContentVersion> _versions = [];

    public ContentItemId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ConnectionId ConnectionId { get; private set; }
    public ContentKey ExternalContentKey { get; private set; } = null!;
    public string ContentType { get; private set; } = string.Empty;
    public LocaleCode SourceLocale { get; private set; } = null!;
    public ContentVersionId? LatestVersionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<ContentVersion> Versions => _versions.AsReadOnly();

    private ContentItem() { }

    public ContentItem(
        ContentItemId id,
        TenantId tenantId,
        ConnectionId connectionId,
        ContentKey externalContentKey,
        string contentType,
        LocaleCode sourceLocale,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        Id = id;
        TenantId = tenantId;
        ConnectionId = connectionId;
        ExternalContentKey = externalContentKey;
        ContentType = contentType.Trim();
        SourceLocale = sourceLocale;
        CreatedAt = createdAt;
    }

    public void SetLatestVersion(ContentVersionId versionId)
    {
        LatestVersionId = versionId;
    }
}

/// <summary>
/// Immutable snapshot of a content item at a specific CMS version and locale.
/// Never update existing rows; insert a new version row instead.
/// </summary>
public sealed class ContentVersion
{
    private readonly List<ContentBlock> _blocks = [];

    public ContentVersionId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ContentItemId ContentItemId { get; private set; }
    public string ExternalVersionKey { get; private set; } = string.Empty;
    public LocaleCode Locale { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public ContentHash ContentHash { get; private set; } = null!;
    public string RawPayload { get; private set; } = "{}";
    public DateTimeOffset CapturedAt { get; private set; }

    public IReadOnlyCollection<ContentBlock> Blocks => _blocks.AsReadOnly();

    private ContentVersion() { }

    public ContentVersion(
        ContentVersionId id,
        TenantId tenantId,
        ContentItemId contentItemId,
        string externalVersionKey,
        LocaleCode locale,
        int versionNumber,
        ContentHash contentHash,
        string rawPayload,
        DateTimeOffset capturedAt,
        IEnumerable<ContentBlock>? blocks = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalVersionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPayload);
        if (versionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber));
        }

        Id = id;
        TenantId = tenantId;
        ContentItemId = contentItemId;
        ExternalVersionKey = externalVersionKey.Trim();
        Locale = locale;
        VersionNumber = versionNumber;
        ContentHash = contentHash;
        RawPayload = rawPayload;
        CapturedAt = capturedAt;

        if (blocks is not null)
        {
            _blocks.AddRange(blocks);
        }
    }
}

public sealed class ContentBlock
{
    public ContentBlockId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ContentVersionId ContentVersionId { get; private set; }
    public BlockKey BlockKey { get; private set; } = null!;
    public string FieldPath { get; private set; } = string.Empty;
    public string BlockType { get; private set; } = string.Empty;
    public string SourceText { get; private set; } = string.Empty;
    public ContentHash ContentHash { get; private set; } = null!;
    public int Ordinal { get; private set; }

    private ContentBlock() { }

    public ContentBlock(
        ContentBlockId id,
        TenantId tenantId,
        ContentVersionId contentVersionId,
        BlockKey blockKey,
        string fieldPath,
        string blockType,
        string sourceText,
        ContentHash contentHash,
        int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);

        Id = id;
        TenantId = tenantId;
        ContentVersionId = contentVersionId;
        BlockKey = blockKey;
        FieldPath = fieldPath.Trim();
        BlockType = blockType.Trim();
        SourceText = sourceText ?? string.Empty;
        ContentHash = contentHash;
        Ordinal = ordinal;
    }
}

public sealed class Translation
{
    public TranslationId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ContentItemId ContentItemId { get; private set; }
    public BlockKey BlockKey { get; private set; } = null!;
    public LocaleCode TargetLocale { get; private set; } = null!;
    public ContentHash SourceHash { get; private set; } = null!;
    public string TranslatedText { get; private set; } = string.Empty;
    public TranslationProvenance Provenance { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Translation() { }

    public Translation(
        TranslationId id,
        TenantId tenantId,
        ContentItemId contentItemId,
        BlockKey blockKey,
        LocaleCode targetLocale,
        ContentHash sourceHash,
        string translatedText,
        TranslationProvenance provenance,
        DateTimeOffset updatedAt)
    {
        Id = id;
        TenantId = tenantId;
        ContentItemId = contentItemId;
        BlockKey = blockKey;
        TargetLocale = targetLocale;
        SourceHash = sourceHash;
        TranslatedText = translatedText ?? string.Empty;
        Provenance = provenance;
        UpdatedAt = updatedAt;
    }

    public void UpdateTranslation(
        ContentHash newSourceHash,
        string newTranslatedText,
        TranslationProvenance newProvenance,
        DateTimeOffset updatedAt)
    {
        SourceHash = newSourceHash;
        TranslatedText = newTranslatedText;
        Provenance = newProvenance;
        UpdatedAt = updatedAt;
    }
}
