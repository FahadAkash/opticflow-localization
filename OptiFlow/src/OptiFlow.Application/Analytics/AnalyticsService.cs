using OptiFlow.Application.Abstractions;
using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;

namespace OptiFlow.Application.Analytics;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IOptiFlowRepository _repository;
    private readonly ITenantContext _tenantContext;

    public AnalyticsService(IOptiFlowRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<CostAnalyticsResponse> GetCostAnalyticsAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return new CostAnalyticsResponse(0, 0, 0, 0, 0, 0, 0, 0, 0, 0m, 0m);
        }

        IReadOnlyList<LocalizationJob> jobs = await _repository
            .ListJobsAsync(_tenantContext.CurrentTenantId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AiInvocation> invocations = await _repository
            .ListAiInvocationsAsync(_tenantContext.CurrentTenantId, cancellationToken)
            .ConfigureAwait(false);

        int totalBlocks = jobs.Sum(j => j.Blocks.Count);
        int reusedBlocks = jobs.Sum(j => j.Blocks.Count(b =>
            b.ResolutionAction is BlockResolutionAction.ReusedUnchanged
                or BlockResolutionAction.PreservedHumanEdit
                or BlockResolutionAction.ReusedTranslationMemory));
        int preservedHumanBlocks = jobs.Sum(j => j.Blocks.Count(b =>
            b.ResolutionAction is BlockResolutionAction.PreservedHumanEdit
                or BlockResolutionAction.ReusedUnchanged));

        long incInput = invocations.Sum(i => (long)i.InputTokens);
        long incOutput = invocations.Sum(i => (long)i.OutputTokens);
        decimal cloudEquivalent = invocations.Sum(i => i.EstimatedCost);

        // Estimate full-page baseline tokens proportional to total blocks vs AI-processed blocks
        int aiBlockCount = Math.Max(1, jobs.Sum(j => j.Blocks.Count(b => b.ResolutionAction == BlockResolutionAction.AiTranslated)));
        long totalIncTokens = incInput + incOutput;
        long estimatedBaseline = totalBlocks == 0 || totalIncTokens == 0
            ? totalBlocks * 150L
            : (long)Math.Round(totalIncTokens * ((double)totalBlocks / aiBlockCount));

        double reuseRate = totalBlocks == 0 ? 0.0 : Math.Round((double)reusedBlocks / totalBlocks * 100.0, 2);
        double preservationRate = totalBlocks == 0 ? 100.0 : Math.Round((double)preservedHumanBlocks / totalBlocks * 100.0, 2);
        double reductionPercent = estimatedBaseline == 0
            ? 0.0
            : Math.Round(Math.Max(0.0, (1.0 - ((double)totalIncTokens / estimatedBaseline)) * 100.0), 2);

        return new CostAnalyticsResponse(
            TotalJobs: jobs.Count,
            TotalBlocksProcessed: totalBlocks,
            BlocksReusedOrPreserved: reusedBlocks,
            TranslationReuseRate: reuseRate,
            ContentPreservationRate: preservationRate,
            IncrementalInputTokens: incInput,
            IncrementalOutputTokens: incOutput,
            EstimatedBaselineTokens: estimatedBaseline,
            AiCreditReductionPercent: reductionPercent,
            EstimatedLocalCostUsd: 0m,
            EstimatedCloudEquivalentCostUsd: cloudEquivalent);
    }

    public async Task<PerformanceAnalyticsResponse> GetPerformanceAnalyticsAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return new PerformanceAnalyticsResponse(0, 0, 0, 0, 0, 0, 0);
        }

        IReadOnlyList<LocalizationJob> jobs = await _repository
            .ListJobsAsync(_tenantContext.CurrentTenantId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<AiInvocation> invocations = await _repository
            .ListAiInvocationsAsync(_tenantContext.CurrentTenantId, cancellationToken)
            .ConfigureAwait(false);

        int succeeded = jobs.Count(j => j.Status is JobStatus.AwaitingReview or JobStatus.Approved or JobStatus.CmsDraftCreated);
        int failed = jobs.Count(j => j.Status is JobStatus.Failed or JobStatus.DeadLetter);
        int finished = succeeded + failed;
        double successRate = finished == 0 ? 100.0 : Math.Round((double)succeeded / finished * 100.0, 2);

        List<double> latencies = invocations
            .Select(i => (double)i.LatencyMs)
            .OrderBy(x => x)
            .ToList();

        return new PerformanceAnalyticsResponse(
            TotalJobs: jobs.Count,
            SucceededJobs: succeeded,
            FailedOrDeadLetterJobs: failed,
            JobSuccessRate: successRate,
            LatencyP50Ms: Percentile(latencies, 0.50),
            LatencyP95Ms: Percentile(latencies, 0.95),
            LatencyP99Ms: Percentile(latencies, 0.99));
    }

    private static double Percentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0.0;
        }

        int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}
