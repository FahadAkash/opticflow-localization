using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptiFlow.Application.Abstractions;
using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Interfaces;

namespace OptiFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/cms/connections")]
public sealed class CmsConnectionsController : ControllerBase
{
    private readonly ICmsConnectionService _service;
    private readonly IValidator<CreateConnectionRequest> _validator;

    public CmsConnectionsController(
        ICmsConnectionService service,
        IValidator<CreateConnectionRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConnectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateConnectionRequest request,
        CancellationToken cancellationToken)
    {
        ValidationResult validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiError.BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        Result<ConnectionResponse> result = await _service.CreateAsync(request, cancellationToken);
        return result.Match<IActionResult>(
            conn => CreatedAtAction(nameof(List), new { id = conn.Id }, conn),
            (error, code) => code == ErrorCodes.PermissionDenied
                ? Forbid()
                : BadRequest(ApiError.BadRequest(error, code)));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<ConnectionResponse> items = await _service.ListAsync(cancellationToken);
        return Ok(items);
    }
}

[ApiController]
[Authorize]
[Route("api/v1/content")]
public sealed class ContentController : ControllerBase
{
    private readonly IContentSyncService _syncService;
    private readonly IOptiFlowRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IValidator<SyncContentRequest> _syncValidator;

    public ContentController(
        IContentSyncService syncService,
        IOptiFlowRepository repository,
        ITenantContext tenantContext,
        IValidator<SyncContentRequest> syncValidator)
    {
        _syncService = syncService;
        _repository = repository;
        _tenantContext = tenantContext;
        _syncValidator = syncValidator;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListItems(CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Forbid();
        }

        IReadOnlyList<ContentItem> items = await _repository.ListContentItemsAsync(_tenantContext.CurrentTenantId, cancellationToken);
        var payload = items.Select(i => new
        {
            id = i.Id.Value,
            connectionId = i.ConnectionId.Value,
            externalContentKey = i.ExternalContentKey.Value,
            contentType = i.ContentType,
            sourceLocale = i.SourceLocale.Value,
            latestVersionId = i.LatestVersionId?.Value,
            createdAt = i.CreatedAt
        });
        return Ok(payload);
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncContentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sync(
        [FromBody] SyncContentRequest request,
        CancellationToken cancellationToken)
    {
        ValidationResult validation = await _syncValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiError.BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        Result<SyncContentResponse> result = await _syncService.SyncAsync(request, cancellationToken);
        return result.Match<IActionResult>(
            synced => AcceptedAtAction(nameof(ListVersions), new { id = synced.ContentItemId }, synced),
            (error, code) => code == ErrorCodes.NotFound
                ? NotFound(ApiError.NotFound(error))
                : BadRequest(ApiError.BadRequest(error, code)));
    }

    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<ContentVersionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListVersions(Guid id, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ContentVersionResponse>> result = await _syncService.ListVersionsAsync(id, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            (error, code) => code == ErrorCodes.NotFound
                ? NotFound(ApiError.NotFound("ContentItem"))
                : BadRequest(ApiError.BadRequest(error, code)));
    }

    [HttpGet("{id:guid}/diff")]
    [ProducesResponseType(typeof(DiffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Diff(
        Guid id,
        [FromQuery] Guid? fromVersionId,
        [FromQuery] Guid? toVersionId,
        CancellationToken cancellationToken)
    {
        Result<DiffResponse> result = await _syncService.DiffAsync(id, fromVersionId, toVersionId, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            (error, code) => code == ErrorCodes.NotFound
                ? NotFound(ApiError.NotFound("ContentVersion"))
                : BadRequest(ApiError.BadRequest(error, code)));
    }
}
