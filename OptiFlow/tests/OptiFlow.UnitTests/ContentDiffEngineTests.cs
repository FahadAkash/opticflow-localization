using FluentAssertions;
using OptiFlow.Application.Diff;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;
using Xunit;

namespace OptiFlow.UnitTests;

public sealed class ContentDiffEngineTests
{
    private readonly ContentDiffEngine _sut = new();

    [Fact]
    public void Compare_WhitespaceDifferencesOnly_ProducesIdenticalHashAndUnchanged()
    {
        ContentSnapshot prev = CreateSnapshot("v1", [new("b1", "hero.title", "text", "Hello   world\r\nnow", 0)]);
        ContentSnapshot curr = CreateSnapshot("v2", [new("b1", "hero.title", "text", "  Hello world now ", 0)]);

        ContentDiffResult result = _sut.Compare(prev, curr);

        result.UnchangedCount.Should().Be(1);
        result.ModifiedCount.Should().Be(0);
        result.Blocks.Single().Action.Should().Be(BlockDiffAction.Unchanged);
        result.Blocks.Single().SourceHash.Should().Be(ContentHash.FromRawText("Hello world now"));
    }

    [Fact]
    public void Compare_ReorderedBlocksWithSameBlockKey_ClassifiesAsUnchanged()
    {
        ContentSnapshot prev = CreateSnapshot("v1", [
            new("b1", "sections[0].text", "text", "First", 0),
            new("b2", "sections[1].text", "text", "Second", 1)
        ]);
        ContentSnapshot curr = CreateSnapshot("v2", [
            new("b2", "sections[0].text", "text", "Second", 0),
            new("b1", "sections[1].text", "text", "First", 1)
        ]);

        ContentDiffResult result = _sut.Compare(prev, curr);

        result.UnchangedCount.Should().Be(2);
        result.ModifiedCount.Should().Be(0);
        result.AddedCount.Should().Be(0);
        result.DeletedCount.Should().Be(0);
    }

    [Fact]
    public void Compare_BlockKeyChangedButSameFieldPath_FallsBackToFieldPath()
    {
        ContentSnapshot prev = CreateSnapshot("v1", [new("old-key", "hero.subtitle", "text", "Original subtitle", 0)]);
        ContentSnapshot curr = CreateSnapshot("v2", [new("new-key", "hero.subtitle", "text", "Original subtitle", 0)]);

        ContentDiffResult result = _sut.Compare(prev, curr);

        result.UnchangedCount.Should().Be(1);
        result.AddedCount.Should().Be(0);
        result.DeletedCount.Should().Be(0);
    }

    [Fact]
    public void Compare_MixedModifiedAddedDeletedUnchanged_ReportsExactCounts()
    {
        ContentSnapshot prev = CreateSnapshot("v1", [
            new("b1", "p[0]", "text", "Stay same", 0),
            new("b2", "p[1]", "text", "Old text", 1),
            new("b3", "p[2]", "text", "Removed text", 2)
        ]);
        ContentSnapshot curr = CreateSnapshot("v2", [
            new("b1", "p[0]", "text", "Stay same", 0),
            new("b2", "p[1]", "text", "Updated text", 1),
            new("b4", "p[3]", "text", "Brand new block", 2)
        ]);

        ContentDiffResult result = _sut.Compare(prev, curr);

        result.UnchangedCount.Should().Be(1);
        result.ModifiedCount.Should().Be(1);
        result.AddedCount.Should().Be(1);
        result.DeletedCount.Should().Be(1);
        result.Blocks.Should().Contain(b => b.BlockKey == "b3" && b.Action == BlockDiffAction.Deleted);
        result.Blocks.Should().Contain(b => b.BlockKey == "b4" && b.Action == BlockDiffAction.Added);
    }

    private static ContentSnapshot CreateSnapshot(string versionKey, IReadOnlyList<ContentBlockSnapshot> blocks) =>
        new("page-1", versionKey, "en", 1, "Article", "{}", blocks);
}
