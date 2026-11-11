using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Common;

namespace OptiFlow.Application.Abstractions;

public interface ICmsConnectionService
{
    Task<Result<ConnectionResponse>> CreateAsync(
        CreateConnectionRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConnectionResponse>> ListAsync(CancellationToken cancellationToken);
}

public interface IContentSyncService
{
    Task<Result<SyncContentResponse>> SyncAsync(
        SyncContentRequest request,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<ContentVersionResponse>>> ListVersionsAsync(
        Guid contentItemId,
        CancellationToken cancellationToken);

    Task<Result<DiffResponse>> DiffAsync(
        Guid contentItemId,
        Guid? fromVersionId,
        Guid? toVersionId,
        CancellationToken cancellationToken);
}

public interface IReviewService
{
    Task<IReadOnlyList<ReviewTaskResponse>> ListPendingAsync(CancellationToken cancellationToken);

    Task<Result<ReviewDecisionResponse>> ApproveAsync(
        Guid reviewId,
        ApproveReviewRequest request,
        CancellationToken cancellationToken);

    Task<Result<ReviewDecisionResponse>> RejectAsync(
        Guid reviewId,
        RejectReviewRequest request,
        CancellationToken cancellationToken);
}

public interface IAnalyticsService
{
    Task<CostAnalyticsResponse> GetCostAnalyticsAsync(CancellationToken cancellationToken);
    Task<PerformanceAnalyticsResponse> GetPerformanceAnalyticsAsync(CancellationToken cancellationToken);
}

public interface IWebhookIngestionService
{
    Task<Result<WebhookIngestResponse>> IngestAsync(
        OptimizelyWebhookRequest request,
        string rawPayload,
        string? signatureHeader,
        CancellationToken cancellationToken);
}
