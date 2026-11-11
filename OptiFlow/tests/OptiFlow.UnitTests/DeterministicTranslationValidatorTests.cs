using FluentAssertions;
using Moq;
using OptiFlow.Application.Validation;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;
using Xunit;

namespace OptiFlow.UnitTests;

public sealed class DeterministicTranslationValidatorTests
{
    private readonly TenantId _tenantId = TenantId.New();
    private readonly ContentItemId _itemId = ContentItemId.New();
    private readonly ContentVersionId _versionId = ContentVersionId.New();

    private DeterministicTranslationValidator CreateSut(bool sourceIsCurrent = true)
    {
        var item = new ContentItem(
            _itemId, _tenantId, ConnectionId.New(), new ContentKey("doc-1"),
            "LandingPage", new LocaleCode("en"), DateTimeOffset.UtcNow);
        if (sourceIsCurrent)
        {
            item.SetLatestVersion(_versionId);
        }

        var repo = new Mock<IOptiFlowRepository>();
        repo.Setup(r => r.GetContentItemAsync(_tenantId, _itemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var tenantCtx = new Mock<ITenantContext>();
        tenantCtx.Setup(t => t.CurrentTenantId).Returns(_tenantId);

        return new DeterministicTranslationValidator(repo.Object, tenantCtx.Object);
    }

    private static TranslationResult MakeAiResult(string? text, double confidence = 0.99, bool success = true) =>
        new(success, "{}", text, confidence, 20, 15, 100, "gemma-4b", "LMStudio",
            AiInvocationStatus.Succeeded, IsTransientError: false);

    [Fact]
    public async Task ValidateAsync_AllDeterministicChecksPass_ReturnsValid()
    {
        DeterministicTranslationValidator sut = CreateSut();
        IReadOnlyList<GlossaryPair> glossary = [new("Luxury", "de lujo", "barato")];

        ValidationBreakdown result = await sut.ValidateAsync(
            "<p>Welcome to {brand} luxury hotel</p>",
            MakeAiResult("<p>Bienvenido al hotel de lujo {brand}</p>"),
            glossary,
            _itemId,
            _versionId,
            CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.Checks.Should().HaveCount(7).And.OnlyContain(c => c.Passed);
    }

    [Fact]
    public async Task ValidateAsync_HighConfidenceButBrokenHtmlTags_FailsValidation()
    {
        DeterministicTranslationValidator sut = CreateSut();

        ValidationBreakdown result = await sut.ValidateAsync(
            "<p>Book <strong>now</strong></p>",
            MakeAiResult("<p>Reserva ahora</p>", confidence: 0.99),
            [],
            _itemId,
            _versionId,
            CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Checks.Single(c => c.CheckName == "HtmlTagParity").Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_MissingPlaceholder_FailsValidation()
    {
        DeterministicTranslationValidator sut = CreateSut();

        ValidationBreakdown result = await sut.ValidateAsync(
            "Hello {{guestName}}, your room is {roomNumber}.",
            MakeAiResult("Hola {{guestName}}, su habitacion esta lista."),
            [],
            _itemId,
            _versionId,
            CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Checks.Single(c => c.CheckName == "PlaceholderParity").Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ForbiddenGlossaryTermUsed_FailsValidation()
    {
        DeterministicTranslationValidator sut = CreateSut();
        IReadOnlyList<GlossaryPair> glossary = [new("Luxury", "de lujo", "economico")];

        ValidationBreakdown result = await sut.ValidateAsync(
            "Enjoy our luxury spa.",
            MakeAiResult("Disfruta nuestro spa de lujo economico."),
            glossary,
            _itemId,
            _versionId,
            CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Checks.Single(c => c.CheckName == "GlossaryCompliance").Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_AbnormalLengthGrowth_FailsValidation()
    {
        DeterministicTranslationValidator sut = CreateSut();

        ValidationBreakdown result = await sut.ValidateAsync(
            "Book now",
            MakeAiResult(new string('a', 100)),
            [],
            _itemId,
            _versionId,
            CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Checks.Single(c => c.CheckName == "LengthGuard").Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_StaleSourceVersion_FailsValidation()
    {
        DeterministicTranslationValidator sut = CreateSut(sourceIsCurrent: false);

        ValidationBreakdown result = await sut.ValidateAsync(
            "Welcome",
            MakeAiResult("Bienvenido"),
            [],
            _itemId,
            _versionId,
            CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Checks.Single(c => c.CheckName == "SourceVersionCurrent").Passed.Should().BeFalse();
    }
}
