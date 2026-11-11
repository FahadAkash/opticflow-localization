using OptiFlow.Application.Abstractions;
using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Opal.Tools;

/// <summary>
/// Opal custom tool handlers (P1).
/// Strictly delegates to Application services and never touches the database or CMS directly.
/// </summary>
public sealed class AnalyzeContentChangesTool
{
    private readonly ILocalizationOrchestrator _orchestrator;

    public AnalyzeContentChangesTool(ILocalizationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public Task<Result<AnalysisResult>> ExecuteAsync(
        Guid contentItemId,
        string targetLocale,
        CancellationToken cancellationToken) =>
        _orchestrator.AnalyzeAsync(contentItemId, targetLocale, cancellationToken);
}

public sealed class TranslateContentDeltaTool
{
    private readonly ILocalizationOrchestrator _orchestrator;
    private readonly ITenantContext _tenantContext;

    public TranslateContentDeltaTool(
        ILocalizationOrchestrator orchestrator,
        ITenantContext tenantContext)
    {
        _orchestrator = orchestrator;
        _tenantContext = tenantContext;
    }

    public Task<Result<LocalizationJob>> ExecuteAsync(
        Guid contentItemId,
        Guid sourceVersionId,
        string targetLocale,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Task.FromResult(Result<LocalizationJob>.Failure("Tenant context is required.", ErrorCodes.PermissionDenied));
        }

        return _orchestrator.CreateJobAsync(
            _tenantContext.CurrentTenantId,
            new ContentItemId(contentItemId),
            new ContentVersionId(sourceVersionId),
            targetLocale,
            idempotencyKey,
            priority: 1,
            cancellationToken);
    }
}

public sealed class GetLocalizationReportTool
{
    private readonly IAnalyticsService _analyticsService;

    public GetLocalizationReportTool(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public async Task<OpalLocalizationReport> ExecuteAsync(CancellationToken cancellationToken)
    {
        CostAnalyticsResponse costs = await _analyticsService.GetCostAnalyticsAsync(cancellationToken).ConfigureAwait(false);
        PerformanceAnalyticsResponse perf = await _analyticsService.GetPerformanceAnalyticsAsync(cancellationToken).ConfigureAwait(false);
        return new OpalLocalizationReport(costs, perf);
    }
}

public sealed record OpalLocalizationReport(
    CostAnalyticsResponse Costs,
    PerformanceAnalyticsResponse Performance);
