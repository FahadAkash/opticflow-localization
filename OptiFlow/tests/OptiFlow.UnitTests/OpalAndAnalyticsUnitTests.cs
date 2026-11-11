using FluentAssertions;
using Moq;
using OptiFlow.Application.Abstractions;
using OptiFlow.Application.Analytics;
using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;
using OptiFlow.Opal.Tools;
using Xunit;

namespace OptiFlow.UnitTests;

public sealed class OpalAndAnalyticsUnitTests
{
    private readonly TenantId _tenantId = TenantId.New();

    [Fact]
    public async Task OpalTools_DelegateToApplicationInterfacesOnly()
    {
        var orchestrator = new Mock<ILocalizationOrchestrator>();
        var analytics = new Mock<IAnalyticsService>();
        var tenantCtx = new Mock<ITenantContext>();
        tenantCtx.Setup(t => t.HasTenant).Returns(true);
        tenantCtx.Setup(t => t.CurrentTenantId).Returns(_tenantId);

        var itemId = ContentItemId.New();
        var versionId = ContentVersionId.New();
        var analysis = new AnalysisResult(itemId, versionId, 10, 8, 1, 1, 0, []);
        orchestrator.Setup(o => o.AnalyzeAsync(itemId.Value, "es-ES", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AnalysisResult>.Success(analysis));

        var job = new LocalizationJob(
            JobId.New(), _tenantId, itemId, versionId, null,
            new LocaleCode("es-ES"), new IdempotencyKey("opal-1"), DateTimeOffset.UtcNow);
        orchestrator.Setup(o => o.CreateJobAsync(
                _tenantId, itemId, versionId, "es-ES", "opal-1", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LocalizationJob>.Success(job));

        analytics.Setup(a => a.GetCostAnalyticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CostAnalyticsResponse(1, 10, 9, 90, 100, 30, 20, 500, 90, 0m, 0.001m));
        analytics.Setup(a => a.GetPerformanceAnalyticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PerformanceAnalyticsResponse(1, 1, 0, 100, 20, 35, 40));

        var analyzeTool = new AnalyzeContentChangesTool(orchestrator.Object);
        var translateTool = new TranslateContentDeltaTool(orchestrator.Object, tenantCtx.Object);
        var reportTool = new GetLocalizationReportTool(analytics.Object);

        Result<AnalysisResult> analyzeRes = await analyzeTool.ExecuteAsync(itemId.Value, "es-ES", CancellationToken.None);
        Result<LocalizationJob> jobRes = await translateTool.ExecuteAsync(
            itemId.Value, versionId.Value, "es-ES", "opal-1", CancellationToken.None);
        OpalLocalizationReport report = await reportTool.ExecuteAsync(CancellationToken.None);

        analyzeRes.Value!.BlocksRequiringAi.Should().Be(1);
        jobRes.Value!.Id.Should().Be(job.Id);
        report.Costs.TranslationReuseRate.Should().Be(90);
        report.Performance.JobSuccessRate.Should().Be(100);
    }

    [Fact]
    public async Task AnalyticsService_ComputesReuseRateCreditReductionAndPercentiles()
    {
        var repo = new Mock<IOptiFlowRepository>();
        var tenantCtx = new Mock<ITenantContext>();
        tenantCtx.Setup(t => t.HasTenant).Returns(true);
        tenantCtx.Setup(t => t.CurrentTenantId).Returns(_tenantId);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var job = new LocalizationJob(
            JobId.New(), _tenantId, ContentItemId.New(), ContentVersionId.New(), null,
            new LocaleCode("es-ES"), new IdempotencyKey("k"), now);
        job.TransitionTo(JobStatus.AwaitingReview, now);

        var blocks = Enumerable.Range(0, 10).Select(i =>
            new LocalizationJobBlock(
                JobBlockId.New(), _tenantId, job.Id, new BlockKey($"b-{i}"), $"p[{i}]", "text", "src",
                ContentHash.FromRawText("src"),
                i < 9 ? BlockDiffAction.Unchanged : BlockDiffAction.Modified,
                i < 9 ? BlockResolutionAction.ReusedUnchanged : BlockResolutionAction.AiTranslated,
                "OK"))
            .ToList();
        job.ReplaceBlocks(blocks);

        repo.Setup(r => r.ListJobsAsync(_tenantId, It.IsAny<CancellationToken>())).ReturnsAsync([job]);
        repo.Setup(r => r.ListAiInvocationsAsync(_tenantId, It.IsAny<CancellationToken>())).ReturnsAsync([
            new AiInvocation(
                AiInvocationId.New(), _tenantId, job.Id, blocks[9].Id, "LMStudio", "gemma-4b", "v1",
                100, 50, 42, 0.000375m, AiInvocationStatus.Succeeded, now)
        ]);

        var sut = new AnalyticsService(repo.Object, tenantCtx.Object);
        CostAnalyticsResponse cost = await sut.GetCostAnalyticsAsync(CancellationToken.None);
        PerformanceAnalyticsResponse perf = await sut.GetPerformanceAnalyticsAsync(CancellationToken.None);

        cost.TranslationReuseRate.Should().Be(90.0);
        cost.AiCreditReductionPercent.Should().Be(90.0);
        cost.EstimatedLocalCostUsd.Should().Be(0m);
        perf.LatencyP50Ms.Should().Be(42);
        perf.JobSuccessRate.Should().Be(100.0);
    }
}
