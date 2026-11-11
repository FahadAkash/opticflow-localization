using System.Text;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptiFlow.Application.Abstractions;
using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/localization")]
public sealed class LocalizationController : ControllerBase
{
    private readonly ILocalizationOrchestrator _orchestrator;
    private readonly IOptiFlowRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IValidator<CreateJobRequest> _validator;

    public LocalizationController(
        ILocalizationOrchestrator orchestrator,
        IOptiFlowRepository repository,
        ITenantContext tenantContext,
        IValidator<CreateJobRequest> validator)
    {
        _orchestrator = orchestrator;
        _repository = repository;
        _tenantContext = tenantContext;
        _validator = validator;
    }

    [HttpPost("jobs")]
    [ProducesResponseType(typeof(CreateJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateJobs(
        [FromBody] CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Forbid();
        }

        ValidationResult validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiError.BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var created = new List<JobStatusResponse>(request.TargetLocales.Count);
        foreach (string locale in request.TargetLocales.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string key = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? $"job:{request.ContentId}:{request.SourceVersionId}:{locale}:{Guid.NewGuid():N}"
                : $"{request.IdempotencyKey}:{locale}";

            Result<LocalizationJob> result = await _orchestrator.CreateJobAsync(
                _tenantContext.CurrentTenantId,
                new ContentItemId(request.ContentId),
                new ContentVersionId(request.SourceVersionId),
                locale,
                key,
                request.Priority,
                cancellationToken);

            if (result.IsFailure)
            {
                return result.ErrorCode == ErrorCodes.NotFound
                    ? NotFound(ApiError.NotFound(result.Error!))
                    : BadRequest(ApiError.BadRequest(result.Error!, result.ErrorCode!));
            }

            created.Add(ToResponse(result.Value!));
        }

        return Accepted(new CreateJobResponse(created));
    }

    [HttpGet("jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<JobStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListJobs(CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Forbid();
        }

        IReadOnlyList<LocalizationJob> jobs = await _repository.ListJobsAsync(_tenantContext.CurrentTenantId, cancellationToken);
        return Ok(jobs.Select(ToResponse).ToList());
    }

    [HttpGet("jobs/{id:guid}")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid id, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Forbid();
        }

        LocalizationJob? job = await _repository.GetJobAsync(_tenantContext.CurrentTenantId, new JobId(id), cancellationToken);
        return job is null
            ? NotFound(ApiError.NotFound("LocalizationJob"))
            : Ok(ToResponse(job));
    }

    [HttpGet("jobs/{id:guid}/blocks")]
    [ProducesResponseType(typeof(IReadOnlyList<JobBlockResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobBlocks(Guid id, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Forbid();
        }

        LocalizationJob? job = await _repository.GetJobAsync(_tenantContext.CurrentTenantId, new JobId(id), cancellationToken);
        if (job is null)
        {
            return NotFound(ApiError.NotFound("LocalizationJob"));
        }

        var blocks = job.Blocks.Select(b => new JobBlockResponse(
            b.Id.Value,
            b.BlockKey.Value,
            b.FieldPath,
            b.BlockType,
            b.SourceText,
            b.SourceHash.Value,
            b.ActionType,
            b.ResolutionAction,
            b.Status,
            b.PreviousTranslatedText,
            b.TranslatedText,
            b.TranslationMemoryId?.Value,
            b.HasHumanEditConflict,
            b.ValidationResult)).ToList();

        return Ok(blocks);
    }

    private static JobStatusResponse ToResponse(LocalizationJob job) =>
        new(
            job.Id.Value,
            job.ContentItemId.Value,
            job.SourceVersionId.Value,
            job.TargetLocale.Value,
            job.Status,
            job.AttemptCount,
            job.MaxAttempts,
            job.LastErrorCode,
            job.CreatedAt,
            job.CompletedAt);
}

[ApiController]
[Authorize]
[Route("api/v1/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ReviewTaskResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPending(CancellationToken cancellationToken)
    {
        IReadOnlyList<ReviewTaskResponse> pending = await _reviewService.ListPendingAsync(cancellationToken);
        return Ok(pending);
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ReviewDecisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveReviewRequest request,
        CancellationToken cancellationToken)
    {
        Result<ReviewDecisionResponse> result = await _reviewService.ApproveAsync(id, request, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            (error, code) => code switch
            {
                ErrorCodes.StaleConflict => Conflict(ApiError.Conflict(error, ErrorCodes.StaleConflict)),
                ErrorCodes.NotFound => NotFound(ApiError.NotFound("ReviewTask")),
                _ => BadRequest(ApiError.BadRequest(error, code))
            });
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ReviewDecisionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectReviewRequest request,
        CancellationToken cancellationToken)
    {
        Result<ReviewDecisionResponse> result = await _reviewService.RejectAsync(id, request, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            (error, code) => code == ErrorCodes.NotFound
                ? NotFound(ApiError.NotFound("ReviewTask"))
                : BadRequest(ApiError.BadRequest(error, code)));
    }
}

[ApiController]
[Authorize]
[Route("api/v1/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("costs")]
    [ProducesResponseType(typeof(CostAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCosts(CancellationToken cancellationToken) =>
        Ok(await _analyticsService.GetCostAnalyticsAsync(cancellationToken));

    [HttpGet("performance")]
    [ProducesResponseType(typeof(PerformanceAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPerformance(CancellationToken cancellationToken) =>
        Ok(await _analyticsService.GetPerformanceAnalyticsAsync(cancellationToken));
}

[ApiController]
[AllowAnonymous]
[Route("api/v1/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly IWebhookIngestionService _webhookService;
    private readonly ITenantContext _tenantContext;
    private readonly IValidator<OptimizelyWebhookRequest> _validator;

    public WebhooksController(
        IWebhookIngestionService webhookService,
        ITenantContext tenantContext,
        IValidator<OptimizelyWebhookRequest> validator)
    {
        _webhookService = webhookService;
        _tenantContext = tenantContext;
        _validator = validator;
    }

    [HttpPost("optimizely")]
    [ProducesResponseType(typeof(WebhookIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceiveOptimizely(
        [FromHeader(Name = "X-OptiFlow-Signature")] string? signatureHeader,
        [FromHeader(Name = "X-OptiFlow-Tenant-Id")] string? tenantHeader,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            if (Guid.TryParse(tenantHeader, out Guid parsedTenantId) && parsedTenantId != Guid.Empty)
            {
                _tenantContext.SetTenant(new TenantId(parsedTenantId), "webhook-provider");
            }
            else
            {
                return Unauthorized(ApiError.BadRequest("Missing or invalid tenant context.", ErrorCodes.PermissionDenied));
            }
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        string rawPayload = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(rawPayload) || rawPayload.Length > 1_048_576)
        {
            return BadRequest(ApiError.BadRequest("Webhook payload is empty or exceeds 1 MB limit."));
        }

        OptimizelyWebhookRequest? request;
        try
        {
            request = System.Text.Json.JsonSerializer.Deserialize<OptimizelyWebhookRequest>(
                rawPayload,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(ApiError.BadRequest("Invalid webhook JSON payload."));
        }

        if (request is null)
        {
            return BadRequest(ApiError.BadRequest("Webhook payload deserialized to null."));
        }

        ValidationResult validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiError.BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        Result<WebhookIngestResponse> result = await _webhookService.IngestAsync(
            request,
            rawPayload,
            signatureHeader,
            cancellationToken);

        return result.Match<IActionResult>(
            Ok,
            (error, code) => code switch
            {
                ErrorCodes.PermissionDenied => Unauthorized(ApiError.BadRequest(error, code)),
                ErrorCodes.NotFound => NotFound(ApiError.NotFound(error)),
                _ => BadRequest(ApiError.BadRequest(error, code))
            });
    }
}
