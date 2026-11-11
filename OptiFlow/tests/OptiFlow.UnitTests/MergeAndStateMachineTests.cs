using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OptiFlow.Application.Merge;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;
using OptiFlow.Infrastructure.AI;
using Xunit;

namespace OptiFlow.UnitTests;

public sealed class MergeAndStateMachineTests
{
    private readonly TenantId _tenantId = TenantId.New();

    [Fact]
    public async Task PlanMergeAsync_AllSevenRules_RoutesEachBlockCorrectly()
    {
        var tm = new Mock<ITranslationMemory>();
        ContentHash modifiedWithTmHash = ContentHash.FromRawText("Modified with TM");
        var approvedTmEntry = TranslationMemoryEntry.CreateApproved(
            TranslationMemoryId.New(), _tenantId, new LocaleCode("en"), new LocaleCode("es-ES"),
            modifiedWithTmHash, "Modificado via TM", "v1", "Page", "reviewer", DateTimeOffset.UtcNow);

        tm.Setup(t => t.FindApprovedAsync(
                It.Is<TranslationMemoryKey>(k => k.SourceHash == modifiedWithTmHash),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvedTmEntry);

        var item = new ContentItem(
            ContentItemId.New(), _tenantId, ConnectionId.New(), new ContentKey("p1"),
            "Page", new LocaleCode("en"), DateTimeOffset.UtcNow);

        var diff = new ContentDiffResult(6, 2, 3, 1, 1, [
            new("b-unchanged-ai", "f0", "text", "Same", ContentHash.FromRawText("Same"), BlockDiffAction.Unchanged, 0),
            new("b-unchanged-human", "f1", "text", "Same H", ContentHash.FromRawText("Same H"), BlockDiffAction.Unchanged, 1),
            new("b-mod-tm", "f2", "text", "Modified with TM", modifiedWithTmHash, BlockDiffAction.Modified, 2),
            new("b-mod-machine", "f3", "text", "Modified machine", ContentHash.FromRawText("Modified machine"), BlockDiffAction.Modified, 3),
            new("b-mod-human", "f4", "text", "Modified human", ContentHash.FromRawText("Modified human"), BlockDiffAction.Modified, 4),
            new("b-added", "f5", "text", "New block", ContentHash.FromRawText("New block"), BlockDiffAction.Added, 5),
            new("b-deleted", "f6", "text", "Old block", ContentHash.FromRawText("Old block"), BlockDiffAction.Deleted, 6)
        ]);

        var existing = new Dictionary<string, ExistingTranslationState>
        {
            ["b-unchanged-ai"] = new("b-unchanged-ai", "Igual", ContentHash.FromRawText("Same"), TranslationProvenance.AiGenerated),
            ["b-unchanged-human"] = new("b-unchanged-human", "Edicion humana", ContentHash.FromRawText("Same H"), TranslationProvenance.HumanEdited),
            ["b-mod-machine"] = new("b-mod-machine", "Prev AI", ContentHash.FromRawText("Old"), TranslationProvenance.AiGenerated),
            ["b-mod-human"] = new("b-mod-human", "Texto manual", ContentHash.FromRawText("Old"), TranslationProvenance.HumanEdited)
        };

        var sut = new MergeEngine(tm.Object);
        IReadOnlyList<MergeBlockDecision> decisions = await sut.PlanMergeAsync(
            _tenantId, item, new LocaleCode("es-ES"), diff, existing, "v1", CancellationToken.None);

        decisions.Single(d => d.BlockKey == "b-unchanged-ai").ResolutionAction.Should().Be(BlockResolutionAction.ReusedUnchanged);
        decisions.Single(d => d.BlockKey == "b-unchanged-human").ResolutionAction.Should().Be(BlockResolutionAction.PreservedHumanEdit);
        decisions.Single(d => d.BlockKey == "b-mod-tm").ResolutionAction.Should().Be(BlockResolutionAction.ReusedTranslationMemory);
        decisions.Single(d => d.BlockKey == "b-mod-machine").RequiresAiTranslation.Should().BeTrue();
        MergeBlockDecision conflict = decisions.Single(d => d.BlockKey == "b-mod-human");
        conflict.ResolutionAction.Should().Be(BlockResolutionAction.ConflictRequiresReview);
        conflict.HasHumanEditConflict.Should().BeTrue();
        conflict.RequiresAiTranslation.Should().BeFalse();
        decisions.Single(d => d.BlockKey == "b-added").RequiresAiTranslation.Should().BeTrue();
        decisions.Single(d => d.BlockKey == "b-deleted").ResolutionAction.Should().Be(BlockResolutionAction.ProposedRemoval);
    }

    [Fact]
    public void LocalizationJob_RejectedJob_CannotBeApprovedOrPublished()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var job = new LocalizationJob(
            JobId.New(), _tenantId, ContentItemId.New(), ContentVersionId.New(), null,
            new LocaleCode("es-ES"), new IdempotencyKey("k1"), now);

        job.TransitionTo(JobStatus.Rejected, now).IsSuccess.Should().BeTrue();
        Result approveAttempt = job.TransitionTo(JobStatus.Approved, now);

        approveAttempt.IsFailure.Should().BeTrue();
        approveAttempt.ErrorCode.Should().Be(ErrorCodes.InvalidState);
    }

    [Fact]
    public void LocalizationJob_ExponentialBackoff_Schedules0s2s4s8sThenDeadLetter()
    {
        DateTimeOffset t0 = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
        var job = new LocalizationJob(
            JobId.New(), _tenantId, ContentItemId.New(), ContentVersionId.New(), null,
            new LocaleCode("es-ES"), new IdempotencyKey("k2"), t0, maxAttempts: 4);

        job.ClaimLease("w1", t0, TimeSpan.FromMinutes(5));
        job.ScheduleRetryOrDeadLetter("ERR", t0, isRetryable: true);
        job.Status.Should().Be(JobStatus.RetryWait);
        job.AvailableAt.Should().Be(t0);

        job.ClaimLease("w1", t0, TimeSpan.FromMinutes(5));
        job.ScheduleRetryOrDeadLetter("ERR", t0, isRetryable: true);
        job.AvailableAt.Should().Be(t0.AddSeconds(2));

        job.ClaimLease("w1", t0.AddSeconds(2), TimeSpan.FromMinutes(5));
        job.ScheduleRetryOrDeadLetter("ERR", t0.AddSeconds(2), isRetryable: true);
        job.AvailableAt.Should().Be(t0.AddSeconds(6));

        job.ClaimLease("w1", t0.AddSeconds(6), TimeSpan.FromMinutes(5));
        job.ScheduleRetryOrDeadLetter("ERR", t0.AddSeconds(6), isRetryable: true);
        job.Status.Should().Be(JobStatus.DeadLetter);
    }

    [Fact]
    public async Task LmStudioClient_InvalidJsonResponse_ReturnsValidationFailedNonTransient()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"choices\":[{\"message\":{\"content\":\"not valid json\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}",
                Encoding.UTF8,
                "application/json")
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:1234") };
        var sut = new LmStudioLanguageModelClient(
            http,
            Options.Create(new LmStudioOptions()),
            NullLogger<LmStudioLanguageModelClient>.Instance);

        TranslationResult result = await sut.TranslateAsync(
            new TranslationRequest("en", "es-ES", "Hotel", "text", "Hello", []),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.IsTransientError.Should().BeFalse();
        result.Status.Should().Be(AiInvocationStatus.ValidationFailed);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
