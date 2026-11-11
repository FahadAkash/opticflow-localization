using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Application.Diff;

public sealed class ContentDiffEngine : IContentDiffEngine
{
    public ContentDiffResult Compare(ContentSnapshot? previous, ContentSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous is null)
        {
            var addedBlocks = current.Blocks
                .OrderBy(b => b.Ordinal)
                .Select(b => new DiffBlockResult(
                    b.BlockKey,
                    b.FieldPath,
                    b.BlockType,
                    b.SourceText,
                    ContentHash.FromRawText(b.SourceText),
                    BlockDiffAction.Added,
                    b.Ordinal))
                .ToList();

            return new ContentDiffResult(
                TotalBlocks: addedBlocks.Count,
                UnchangedCount: 0,
                ModifiedCount: 0,
                AddedCount: addedBlocks.Count,
                DeletedCount: 0,
                Blocks: addedBlocks);
        }

        // Match by stable BlockKey first, falling back to FieldPath
        var prevByKey = previous.Blocks
            .GroupBy(b => b.BlockKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var prevByFieldPath = previous.Blocks
            .GroupBy(b => b.FieldPath, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var matchedPrevKeys = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<DiffBlockResult>(current.Blocks.Count);

        int unchanged = 0;
        int modified = 0;
        int added = 0;

        foreach (ContentBlockSnapshot currentBlock in current.Blocks.OrderBy(b => b.Ordinal))
        {
            ContentHash currentHash = ContentHash.FromRawText(currentBlock.SourceText);

            ContentBlockSnapshot? matchedPrev = null;
            if (prevByKey.TryGetValue(currentBlock.BlockKey, out ContentBlockSnapshot? byKey))
            {
                matchedPrev = byKey;
            }
            else if (prevByFieldPath.TryGetValue(currentBlock.FieldPath, out ContentBlockSnapshot? byPath) &&
                     !matchedPrevKeys.Contains(byPath.BlockKey))
            {
                matchedPrev = byPath;
            }

            if (matchedPrev is null)
            {
                added++;
                results.Add(new DiffBlockResult(
                    currentBlock.BlockKey,
                    currentBlock.FieldPath,
                    currentBlock.BlockType,
                    currentBlock.SourceText,
                    currentHash,
                    BlockDiffAction.Added,
                    currentBlock.Ordinal));
                continue;
            }

            matchedPrevKeys.Add(matchedPrev.BlockKey);
            ContentHash previousHash = ContentHash.FromRawText(matchedPrev.SourceText);

            if (previousHash == currentHash)
            {
                unchanged++;
                results.Add(new DiffBlockResult(
                    currentBlock.BlockKey,
                    currentBlock.FieldPath,
                    currentBlock.BlockType,
                    currentBlock.SourceText,
                    currentHash,
                    BlockDiffAction.Unchanged,
                    currentBlock.Ordinal));
            }
            else
            {
                modified++;
                results.Add(new DiffBlockResult(
                    currentBlock.BlockKey,
                    currentBlock.FieldPath,
                    currentBlock.BlockType,
                    currentBlock.SourceText,
                    currentHash,
                    BlockDiffAction.Modified,
                    currentBlock.Ordinal));
            }
        }

        int deleted = 0;
        foreach (ContentBlockSnapshot prevBlock in previous.Blocks.OrderBy(b => b.Ordinal))
        {
            if (matchedPrevKeys.Contains(prevBlock.BlockKey))
            {
                continue;
            }

            deleted++;
            results.Add(new DiffBlockResult(
                prevBlock.BlockKey,
                prevBlock.FieldPath,
                prevBlock.BlockType,
                prevBlock.SourceText,
                ContentHash.FromRawText(prevBlock.SourceText),
                BlockDiffAction.Deleted,
                prevBlock.Ordinal));
        }

        return new ContentDiffResult(
            TotalBlocks: results.Count,
            UnchangedCount: unchanged,
            ModifiedCount: modified,
            AddedCount: added,
            DeletedCount: deleted,
            Blocks: results);
    }

    public static ContentSnapshot ToSnapshot(ContentItem item, ContentVersion version) =>
        new(
            item.ExternalContentKey.Value,
            version.ExternalVersionKey,
            version.Locale.Value,
            version.VersionNumber,
            item.ContentType,
            version.RawPayload,
            version.Blocks
                .OrderBy(b => b.Ordinal)
                .Select(b => new ContentBlockSnapshot(
                    b.BlockKey.Value,
                    b.FieldPath,
                    b.BlockType,
                    b.SourceText,
                    b.Ordinal))
                .ToList());
}
