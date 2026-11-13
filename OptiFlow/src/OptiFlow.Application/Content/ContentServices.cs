using System.Text.Json;
using Microsoft.Extensions.Logging;
using OptiFlow.Application.Abstractions;
using OptiFlow.Application.Contracts;
using OptiFlow.Application.Diff;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Application.Content;

public sealed class CmsConnectionService : ICmsConnectionService
{
    private readonly IOptiFlowRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICredentialProtector _credentialProtector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CmsConnectionService> _logger;

    public CmsConnectionService(
        IOptiFlowRepository repository,
        ITenantContext tenantContext,
        ICredentialProtector credentialProtector,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<CmsConnectionService> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _credentialProtector = credentialProtector;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<ConnectionResponse>> CreateAsync(
        CreateConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Result<ConnectionResponse>.Failure("Tenant context is missing.", ErrorCodes.PermissionDenied);
        }

        string credentialRef = _credentialProtector.Protect(request.ApiSecret);
        DateTimeOffset now = _timeProvider.GetUtcNow();
        var connection = new CmsConnection(
            ConnectionId.New(),
            _tenantContext.CurrentTenantId,
            request.Provider,
            request.BaseUrl,
            credentialRef,
            now);

        await _repository.AddConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await _repository.AddAuditEventAsync(
            new AuditEvent(
                AuditEventId.New(),
                _tenantContext.CurrentTenantId,
                _tenantContext.CurrentActorId,
                "CMS_CONNECTION_CREATED",
                "CmsConnection",
                connection.Id.Value.ToString(),
                JsonSerializer.Serialize(new { provider = request.Provider.ToString() }),
                now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created CMS connection {ConnectionId} for tenant {TenantId} provider {Provider}",
            connection.Id.Value,
            _tenantContext.CurrentTenantId.Value,
            connection.Provider);

        return Result<ConnectionResponse>.Success(new ConnectionResponse(
            connection.Id.Value,
            connection.Provider,
            connection.BaseUrl,
            connection.Status,
            connection.CreatedAt));
    }

    public async Task<IReadOnlyList<ConnectionResponse>> ListAsync(CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return [];
        }

        IReadOnlyList<CmsConnection> connections = await _repository
            .ListConnectionsAsync(_tenantContext.CurrentTenantId, cancellationToken)
            .ConfigureAwait(false);

        return connections
            .Select(c => new ConnectionResponse(c.Id.Value, c.Provider, c.BaseUrl, c.Status, c.CreatedAt))
            .ToList();
    }
}

public sealed class ContentSyncService : IContentSyncService
{
    private readonly IOptiFlowRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IContentSourceResolver _contentSourceResolver;
    private readonly IContentDiffEngine _diffEngine;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ContentSyncService> _logger;

    public ContentSyncService(
        IOptiFlowRepository repository,
        ITenantContext tenantContext,
        IContentSourceResolver contentSourceResolver,
        IContentDiffEngine diffEngine,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ContentSyncService> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _contentSourceResolver = contentSourceResolver;
        _diffEngine = diffEngine;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<SyncContentResponse>> SyncAsync(
        SyncContentRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Result<SyncContentResponse>.Failure("Tenant context is missing.", ErrorCodes.PermissionDenied);
        }

        TenantId tenantId = _tenantContext.CurrentTenantId;
        var connectionId = new ConnectionId(request.ConnectionId);
        CmsConnection? connection = await _repository
            .GetConnectionAsync(tenantId, connectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null)
        {
            return Result<SyncContentResponse>.Failure("CMS connection not found.", ErrorCodes.NotFound);
        }

        IContentSource contentSource = _contentSourceResolver.Resolve(connection.Provider);
        string targetLocale = string.IsNullOrWhiteSpace(request.Locale) ? "en" : request.Locale.Trim();
        ContentSnapshot? snapshot = await contentSource
            .GetContentAsync(request.ExternalContentKey, targetLocale, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null)
        {
            return Result<SyncContentResponse>.Failure("Content not found in CMS source.", ErrorCodes.NotFound);
        }

        var externalKey = new ContentKey(snapshot.ExternalContentKey);
        var localeCode = new LocaleCode(snapshot.Locale);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        ContentItem? item = await _repository
            .GetContentItemByExternalKeyAsync(tenantId, connectionId, externalKey, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            item = new ContentItem(
                ContentItemId.New(),
                tenantId,
                connectionId,
                externalKey,
                snapshot.ContentType,
                localeCode,
                now);
            await _repository.AddContentItemAsync(item, cancellationToken).ConfigureAwait(false);
        }

        // Immutable snapshot check: if (content_item_id, external_version_key, locale) exists, return existing without mutating!
        ContentVersion? existingVersion = await _repository
            .GetContentVersionByExternalKeyAsync(tenantId, item.Id, snapshot.ExternalVersionKey, localeCode, cancellationToken)
            .ConfigureAwait(false);

        if (existingVersion is not null)
        {
            _logger.LogInformation(
                "Content sync hit existing immutable version {VersionId} for item {ContentItemId}",
                existingVersion.Id.Value,
                item.Id.Value);

            return Result<SyncContentResponse>.Success(new SyncContentResponse(
                item.Id.Value,
                existingVersion.Id.Value,
                item.ExternalContentKey.Value,
                existingVersion.ExternalVersionKey,
                existingVersion.VersionNumber,
                existingVersion.ContentHash.Value,
                existingVersion.Blocks.Count,
                IsNewVersion: false));
        }

        var versionId = ContentVersionId.New();
        var blocks = snapshot.Blocks
            .OrderBy(b => b.Ordinal)
            .Select(b => new ContentBlock(
                ContentBlockId.New(),
                tenantId,
                versionId,
                new BlockKey(b.BlockKey),
                b.FieldPath,
                b.BlockType,
                b.SourceText,
                ContentHash.FromRawText(b.SourceText),
                b.Ordinal))
            .ToList();

        string combinedBlockDigest = string.Join('|', blocks.Select(b => $"{b.BlockKey.Value}:{b.ContentHash.Value}"));
        ContentHash versionHash = ContentHash.FromRawText(combinedBlockDigest);

        var newVersion = new ContentVersion(
            versionId,
            tenantId,
            item.Id,
            snapshot.ExternalVersionKey,
            localeCode,
            snapshot.VersionNumber,
            versionHash,
            snapshot.RawPayloadJson,
            now,
            blocks);

        await _repository.AddContentVersionAsync(newVersion, cancellationToken).ConfigureAwait(false);
        item.SetLatestVersion(newVersion.Id);

        await _repository.AddAuditEventAsync(
            new AuditEvent(
                AuditEventId.New(),
                tenantId,
                _tenantContext.CurrentActorId,
                "CONTENT_SNAPSHOT_CAPTURED",
                "ContentVersion",
                newVersion.Id.Value.ToString(),
                JsonSerializer.Serialize(new
                {
                    contentItemId = item.Id.Value,
                    versionHash = newVersion.ContentHash.Value,
                    blockCount = blocks.Count
                }),
                now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Captured immutable version {VersionId} (hash {ContentHash}, {BlockCount} blocks) for item {ContentItemId}",
            newVersion.Id.Value,
            newVersion.ContentHash.Value,
            blocks.Count,
            item.Id.Value);

        return Result<SyncContentResponse>.Success(new SyncContentResponse(
            item.Id.Value,
            newVersion.Id.Value,
            item.ExternalContentKey.Value,
            newVersion.ExternalVersionKey,
            newVersion.VersionNumber,
            newVersion.ContentHash.Value,
            blocks.Count,
            IsNewVersion: true));
    }

    public async Task<Result<IReadOnlyList<ContentVersionResponse>>> ListVersionsAsync(
        Guid contentItemId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Result<IReadOnlyList<ContentVersionResponse>>.Failure("Tenant context is missing.", ErrorCodes.PermissionDenied);
        }

        TenantId tenantId = _tenantContext.CurrentTenantId;
        var itemId = new ContentItemId(contentItemId);
        ContentItem? item = await _repository.GetContentItemAsync(tenantId, itemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return Result<IReadOnlyList<ContentVersionResponse>>.Failure("Content item not found.", ErrorCodes.NotFound);
        }

        IReadOnlyList<ContentVersion> versions = await _repository
            .ListContentVersionsAsync(tenantId, itemId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ContentVersionResponse> response = versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ContentVersionResponse(
                v.Id.Value,
                v.ExternalVersionKey,
                v.Locale.Value,
                v.VersionNumber,
                v.ContentHash.Value,
                v.Blocks.Count,
                v.CapturedAt))
            .ToList();

        return Result<IReadOnlyList<ContentVersionResponse>>.Success(response);
    }

    public async Task<Result<DiffResponse>> DiffAsync(
        Guid contentItemId,
        Guid? fromVersionId,
        Guid? toVersionId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Result<DiffResponse>.Failure("Tenant context is missing.", ErrorCodes.PermissionDenied);
        }

        TenantId tenantId = _tenantContext.CurrentTenantId;
        var itemId = new ContentItemId(contentItemId);
        ContentItem? item = await _repository.GetContentItemAsync(tenantId, itemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return Result<DiffResponse>.Failure("Content item not found.", ErrorCodes.NotFound);
        }

        IReadOnlyList<ContentVersion> versions = await _repository
            .ListContentVersionsAsync(tenantId, itemId, cancellationToken)
            .ConfigureAwait(false);

        if (versions.Count == 0)
        {
            return Result<DiffResponse>.Failure("No content versions exist.", ErrorCodes.NotFound);
        }

        List<ContentVersion> ordered = versions.OrderBy(v => v.VersionNumber).ToList();
        ContentVersion? current = toVersionId.HasValue
            ? ordered.FirstOrDefault(v => v.Id.Value == toVersionId.Value)
            : ordered[^1];

        if (current is null)
        {
            return Result<DiffResponse>.Failure("Target content version not found.", ErrorCodes.NotFound);
        }

        ContentVersion? previous = fromVersionId.HasValue
            ? ordered.FirstOrDefault(v => v.Id.Value == fromVersionId.Value)
            : ordered.Where(v => v.VersionNumber < current.VersionNumber).LastOrDefault();

        ContentSnapshot? prevSnapshot = previous is null ? null : ContentDiffEngine.ToSnapshot(item, previous);
        ContentSnapshot currSnapshot = ContentDiffEngine.ToSnapshot(item, current);
        ContentDiffResult diff = _diffEngine.Compare(prevSnapshot, currSnapshot);

        return Result<DiffResponse>.Success(new DiffResponse(
            item.Id.Value,
            previous?.Id.Value,
            current.Id.Value,
            diff.TotalBlocks,
            diff.UnchangedCount,
            diff.ModifiedCount,
            diff.AddedCount,
            diff.DeletedCount,
            diff.Blocks.Select(b => new DiffBlockDto(
                b.BlockKey,
                b.FieldPath,
                b.BlockType,
                b.SourceHash.Value,
                b.Action,
                b.Ordinal)).ToList()));
    }

    public async Task<Result<ContentPreviewResponse>> GetPreviewAsync(
        Guid contentItemId,
        string? locale,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Result<ContentPreviewResponse>.Failure("Tenant context is missing.", ErrorCodes.PermissionDenied);
        }

        TenantId tenantId = _tenantContext.CurrentTenantId;
        var itemId = new ContentItemId(contentItemId);
        ContentItem? item = await _repository.GetContentItemAsync(tenantId, itemId, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return Result<ContentPreviewResponse>.Failure("Content item not found.", ErrorCodes.NotFound);
        }

        if (!item.LatestVersionId.HasValue)
        {
            return Result<ContentPreviewResponse>.Failure("No versions found for content item.", ErrorCodes.NotFound);
        }

        ContentVersion? version = await _repository.GetContentVersionAsync(tenantId, item.LatestVersionId.Value, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Result<ContentPreviewResponse>.Failure("Content version not found.", ErrorCodes.NotFound);
        }

        IReadOnlyList<LocaleCode> availableLocalesList = await _repository
            .GetAvailableTranslationLocalesAsync(tenantId, itemId, cancellationToken)
            .ConfigureAwait(false);

        var allLocales = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { item.SourceLocale.Value };
        foreach (LocaleCode loc in availableLocalesList)
        {
            allLocales.Add(loc.Value);
        }

        string activeLocale = string.IsNullOrWhiteSpace(locale) ? item.SourceLocale.Value : locale.Trim();

        IReadOnlyDictionary<string, Translation> translations = activeLocale.Equals(item.SourceLocale.Value, StringComparison.OrdinalIgnoreCase)
            ? new Dictionary<string, Translation>()
            : await _repository.GetTranslationsAsync(tenantId, itemId, new LocaleCode(activeLocale), cancellationToken).ConfigureAwait(false);

        var previewBlocks = new List<ContentPreviewBlockDto>(version.Blocks.Count);
        foreach (ContentBlock block in version.Blocks.OrderBy(b => b.Ordinal))
        {
            string displayText;
            string provenance;

            if (activeLocale.Equals(item.SourceLocale.Value, StringComparison.OrdinalIgnoreCase))
            {
                displayText = block.SourceText;
                provenance = "SourceMaster";
            }
            else if (translations.TryGetValue(block.BlockKey.Value, out Translation? translation))
            {
                displayText = translation.TranslatedText;
                provenance = translation.Provenance.ToString();
            }
            else
            {
                displayText = block.SourceText;
                provenance = "Untranslated";
            }

            previewBlocks.Add(new ContentPreviewBlockDto(
                block.BlockKey.Value,
                block.FieldPath,
                block.BlockType,
                block.SourceText,
                displayText,
                provenance,
                block.Ordinal));
        }

        return Result<ContentPreviewResponse>.Success(new ContentPreviewResponse(
            item.Id.Value,
            item.ExternalContentKey.Value,
            item.ContentType,
            item.SourceLocale.Value,
            activeLocale,
            allLocales.OrderBy(l => l).ToList(),
            previewBlocks));
    }
}

public sealed class WebhookIngestionService : IWebhookIngestionService
{
    private readonly IOptiFlowRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly IContentSyncService _contentSyncService;
    private readonly ILocalizationOrchestrator _orchestrator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookIngestionService> _logger;

    public WebhookIngestionService(
        IOptiFlowRepository repository,
        ITenantContext tenantContext,
        IWebhookSignatureVerifier signatureVerifier,
        IContentSyncService contentSyncService,
        ILocalizationOrchestrator orchestrator,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<WebhookIngestionService> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _signatureVerifier = signatureVerifier;
        _contentSyncService = contentSyncService;
        _orchestrator = orchestrator;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<WebhookIngestResponse>> IngestAsync(
        OptimizelyWebhookRequest request,
        string rawPayload,
        string? signatureHeader,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            return Result<WebhookIngestResponse>.Failure("Tenant context is missing.", ErrorCodes.PermissionDenied);
        }

        TenantId tenantId = _tenantContext.CurrentTenantId;
        var connectionId = new ConnectionId(request.ConnectionId);
        CmsConnection? connection = await _repository
            .GetConnectionAsync(tenantId, connectionId, cancellationToken)
            .ConfigureAwait(false);

        if (connection is null)
        {
            return Result<WebhookIngestResponse>.Failure("CMS connection not found.", ErrorCodes.NotFound);
        }

        if (!_signatureVerifier.VerifySignature(rawPayload, signatureHeader, connection.CredentialRef))
        {
            return Result<WebhookIngestResponse>.Failure("Invalid webhook signature.", ErrorCodes.PermissionDenied);
        }

        WebhookInbox? existing = await _repository
            .GetWebhookAsync(tenantId, request.Provider, request.ExternalEventId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Duplicate webhook {WebhookId} ignored for tenant {TenantId} event {ExternalEventId}",
                existing.Id.Value,
                tenantId.Value,
                request.ExternalEventId);

            return Result<WebhookIngestResponse>.Success(
                new WebhookIngestResponse(existing.Id.Value, Deduplicated: true, CreatedJobId: null));
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        var inbox = new WebhookInbox(
            WebhookInboxId.New(),
            tenantId,
            request.Provider,
            request.ExternalEventId,
            ContentHash.FromRawText(rawPayload),
            now);

        await _repository.AddWebhookAsync(inbox, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        Result<SyncContentResponse> syncResult = await _contentSyncService
            .SyncAsync(new SyncContentRequest(request.ConnectionId, request.ExternalContentKey, request.SourceLocale), cancellationToken)
            .ConfigureAwait(false);

        if (syncResult.IsFailure)
        {
            inbox.MarkFailed(_timeProvider.GetUtcNow());
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<WebhookIngestResponse>.Failure(syncResult.Error!, syncResult.ErrorCode!);
        }

        Guid? createdJobId = null;
        if (!string.IsNullOrWhiteSpace(request.TargetLocale))
        {
            string idempotencyKey = $"wh:{request.Provider}:{request.ExternalEventId}:{request.TargetLocale}";
            Result<LocalizationJob> jobResult = await _orchestrator.CreateJobAsync(
                tenantId,
                new ContentItemId(syncResult.Value!.ContentItemId),
                new ContentVersionId(syncResult.Value.VersionId),
                request.TargetLocale,
                idempotencyKey,
                priority: 1,
                cancellationToken).ConfigureAwait(false);

            if (jobResult.IsSuccess)
            {
                createdJobId = jobResult.Value!.Id.Value;
            }
        }

        inbox.MarkProcessed(_timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<WebhookIngestResponse>.Success(
            new WebhookIngestResponse(inbox.Id.Value, Deduplicated: false, CreatedJobId: createdJobId));
    }
}
