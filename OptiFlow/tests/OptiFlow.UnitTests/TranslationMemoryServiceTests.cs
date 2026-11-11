using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OptiFlow.Application.TranslationMemory;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;
using Xunit;

namespace OptiFlow.UnitTests;

public sealed class TranslationMemoryServiceTests
{
    private readonly TenantId _tenantId = TenantId.New();

    [Fact]
    public async Task FindApprovedAsync_WhenRepositoryReturnsApprovedEntry_CachesWithTenantKeyAndReturnsEntry()
    {
        var repository = new Mock<IOptiFlowRepository>();
        var cache = new Mock<ICacheService>();
        var hash = ContentHash.FromRawText("Welcome");
        var key = new TranslationMemoryKey(_tenantId, "en", "es-ES", hash, "v1", "LandingPage");

        var entry = TranslationMemoryEntry.CreateApproved(
            TranslationMemoryId.New(), _tenantId, new LocaleCode("en"), new LocaleCode("es-ES"),
            hash, "Bienvenido", "v1", "LandingPage", "reviewer-1", DateTimeOffset.UtcNow);

        repository.Setup(r => r.FindApprovedTranslationMemoryAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var sut = new TranslationMemoryService(repository.Object, cache.Object, NullLogger<TranslationMemoryService>.Instance);

        TranslationMemoryEntry? found = await sut.FindApprovedAsync(key, CancellationToken.None);

        found.Should().NotBeNull();
        found!.TranslatedText.Should().Be("Bienvenido");
        found.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        cache.Verify(c => c.SetAsync(
            _tenantId,
            It.Is<string>(k => k.Contains(hash.Value)),
            It.IsAny<object>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindApprovedAsync_WhenRepositoryReturnsPendingCandidate_ReturnsNull()
    {
        var repository = new Mock<IOptiFlowRepository>();
        var cache = new Mock<ICacheService>();
        var hash = ContentHash.FromRawText("Welcome");
        var key = new TranslationMemoryKey(_tenantId, "en", "es-ES", hash, "v1", "LandingPage");

        var pending = TranslationMemoryEntry.CreatePendingAiCandidate(
            TranslationMemoryId.New(), _tenantId, new LocaleCode("en"), new LocaleCode("es-ES"),
            hash, "Bienvenido", "v1", "LandingPage", DateTimeOffset.UtcNow);

        repository.Setup(r => r.FindApprovedTranslationMemoryAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);

        var sut = new TranslationMemoryService(repository.Object, cache.Object, NullLogger<TranslationMemoryService>.Instance);

        TranslationMemoryEntry? found = await sut.FindApprovedAsync(key, CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task SaveApprovedAsync_WhenEntryIsNotHumanApproved_ThrowsInvalidOperationException()
    {
        var repository = new Mock<IOptiFlowRepository>();
        var cache = new Mock<ICacheService>();
        var sut = new TranslationMemoryService(repository.Object, cache.Object, NullLogger<TranslationMemoryService>.Instance);

        var pending = TranslationMemoryEntry.CreatePendingAiCandidate(
            TranslationMemoryId.New(), _tenantId, new LocaleCode("en"), new LocaleCode("es-ES"),
            ContentHash.FromRawText("Hello"), "Hola", "v1", "Page", DateTimeOffset.UtcNow);

        Func<Task> act = () => sut.SaveApprovedAsync(pending, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
