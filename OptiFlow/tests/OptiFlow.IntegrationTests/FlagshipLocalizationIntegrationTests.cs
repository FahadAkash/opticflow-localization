using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;
using OptiFlow.Infrastructure.CMS;
using OptiFlow.Infrastructure.Persistence;
using OptiFlow.Infrastructure.Security;
using Xunit;

namespace OptiFlow.IntegrationTests;

public sealed class FlagshipLocalizationIntegrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions TestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly TestWebApplicationFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task ProcessJob_100BlocksWith3Changed_ZeroAiCallsFor97PreservesHumanEditsAndBlocksDraftOnValidationFailure()
    {
        _factory.SpyLlm.Reset();
        (TenantId tenantId, ConnectionId connectionId) = await _factory.SeedTenantAndConnectionAsync($"flagship-{Guid.NewGuid():N}");
        HttpClient client = _factory.CreateAuthenticatedClient(tenantId.Value);

        string contentKey = $"flagship-page-{Guid.NewGuid():N}";
        LocalTestContentSource localCms = _factory.Services.GetRequiredService<LocalTestContentSource>();

        // 1. Seed v1 with 100 blocks
        List<ContentBlockSnapshot> v1Blocks = Enumerable.Range(0, 100)
            .Select(i => new ContentBlockSnapshot($"blk-{i:D3}", $"sections[{i}].text", "text", $"Master source paragraph {i}", i))
            .ToList();

        localCms.SeedSnapshot(new ContentSnapshot(
            contentKey, "v1-etag", "en", 1, "Article", "{}", v1Blocks));

        HttpResponseMessage syncV1Res = await client.PostAsJsonAsync(
            "/api/v1/content/sync",
            new SyncContentRequest(connectionId.Value, contentKey, "en"));
        syncV1Res.StatusCode.Should().Be(HttpStatusCode.Accepted);
        SyncContentResponse? syncV1 = await syncV1Res.Content.ReadFromJsonAsync<SyncContentResponse>();
        syncV1.Should().NotBeNull();

        // Seed existing translations for all 100 blocks, marking block 10 as HUMAN_EDITED
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ITenantContext tenantCtx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantCtx.SetTenant(tenantId, "seed");
            OptiFlowDbContext db = scope.ServiceProvider.GetRequiredService<OptiFlowDbContext>();

            for (int i = 0; i < 100; i++)
            {
                TranslationProvenance provenance = i == 10
                    ? TranslationProvenance.HumanEdited
                    : TranslationProvenance.AiGenerated;
                string trText = i == 10
                    ? "Edicion manual protegida 10"
                    : $"Traduccion previa {i}";

                await db.Translations.AddAsync(new Translation(
                    TranslationId.New(),
                    tenantId,
                    new ContentItemId(syncV1!.ContentItemId),
                    new BlockKey($"blk-{i:D3}"),
                    new LocaleCode("es-ES"),
                    ContentHash.FromRawText(v1Blocks[i].SourceText),
                    trText,
                    provenance,
                    DateTimeOffset.UtcNow));
            }
            await db.SaveChangesAsync();
        }

        // 2. Advance CMS to v2 where 97 blocks are unchanged and ONLY 3 blocks (90, 91, 92) changed
        List<ContentBlockSnapshot> v2Blocks = v1Blocks
            .Select(b => b.Ordinal is 90 or 91 or 92
                ? b with { SourceText = $"Updated master paragraph {b.Ordinal} v2" }
                : b)
            .ToList();

        localCms.SeedSnapshot(new ContentSnapshot(
            contentKey, "v2-etag", "en", 2, "Article", "{}", v2Blocks));

        HttpResponseMessage syncV2Res = await client.PostAsJsonAsync(
            "/api/v1/content/sync",
            new SyncContentRequest(connectionId.Value, contentKey, "en"));
        syncV2Res.StatusCode.Should().Be(HttpStatusCode.Accepted);
        SyncContentResponse? syncV2 = await syncV2Res.Content.ReadFromJsonAsync<SyncContentResponse>();
        syncV2!.IsNewVersion.Should().BeTrue();

        // 3. Create localization job for v2 and execute orchestrator via worker DI scope
        HttpResponseMessage createJobRes = await client.PostAsJsonAsync(
            "/api/v1/localization/jobs",
            new CreateJobRequest(syncV2.ContentItemId, syncV2.VersionId, ["es-ES"], $"flagship-run1-{Guid.NewGuid():N}"));
        createJobRes.StatusCode.Should().Be(HttpStatusCode.Accepted);
        CreateJobResponse? created = await createJobRes.Content.ReadFromJsonAsync<CreateJobResponse>(TestJsonOptions);
        Guid jobId = created!.Jobs.Single().JobId;

        using (IServiceScope workerScope = _factory.Services.CreateScope())
        {
            IJobQueue queue = workerScope.ServiceProvider.GetRequiredService<IJobQueue>();
            LocalizationJob? claimed = await queue.ClaimNextAsync("test-worker-1", CancellationToken.None);
            claimed.Should().NotBeNull();
            claimed!.Id.Value.Should().Be(jobId);

            ILocalizationOrchestrator orchestrator = workerScope.ServiceProvider.GetRequiredService<ILocalizationOrchestrator>();
            (await orchestrator.ProcessAsync(jobId, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        // Assert: ZERO AI calls for 97 unchanged blocks; ONLY 3 AI calls made
        _factory.SpyLlm.CallCount.Should().Be(3);

        List<JobBlockResponse>? jobBlocks = await client.GetFromJsonAsync<List<JobBlockResponse>>(
            $"/api/v1/localization/jobs/{jobId}/blocks", TestJsonOptions);
        jobBlocks.Should().HaveCount(100);

        JobBlockResponse humanPreserved = jobBlocks!.Single(b => b.BlockKey == "blk-010");
        humanPreserved.ResolutionAction.Should().Be(BlockResolutionAction.PreservedHumanEdit);
        humanPreserved.TranslatedText.Should().Be("Edicion manual protegida 10");

        // 4. Verify validation failure blocks CMS draft creation (same 3 changed blocks, invalid HTML output)
        _factory.SpyLlm.Reset();
        _factory.SpyLlm.ConfigureResponder(_ => new TranslationResult(
            IsSuccess: true,
            RawJson: "{\"translated_text\":\"<script>broken\",\"confidence\":0.99}",
            TranslatedText: "<script>broken", // Fails HTML tag parity against plain text
            Confidence: 0.99,
            InputTokens: 20,
            OutputTokens: 10,
            LatencyMs: 15,
            ModelName: "gemma-4b",
            Provider: "SpyLMStudio",
            Status: AiInvocationStatus.Succeeded,
            IsTransientError: false));

        HttpResponseMessage createInvalidJobRes = await client.PostAsJsonAsync(
            "/api/v1/localization/jobs",
            new CreateJobRequest(syncV2.ContentItemId, syncV2.VersionId, ["es-ES"], $"flagship-fail-{Guid.NewGuid():N}"));
        CreateJobResponse? failCreated = await createInvalidJobRes.Content.ReadFromJsonAsync<CreateJobResponse>(TestJsonOptions);
        Guid invalidJobId = failCreated!.Jobs.Single().JobId;

        using (IServiceScope workerScope = _factory.Services.CreateScope())
        {
            IJobQueue queue = workerScope.ServiceProvider.GetRequiredService<IJobQueue>();
            LocalizationJob? claimed = await queue.ClaimNextAsync("test-worker-2", CancellationToken.None);
            claimed!.Id.Value.Should().Be(invalidJobId);
            ILocalizationOrchestrator orchestrator = workerScope.ServiceProvider.GetRequiredService<ILocalizationOrchestrator>();
            await orchestrator.ProcessAsync(invalidJobId, CancellationToken.None);
        }

        JobStatusResponse? invalidJob = await client.GetFromJsonAsync<JobStatusResponse>($"/api/v1/localization/jobs/{invalidJobId}", TestJsonOptions);
        invalidJob!.Status.Should().Be(JobStatus.NeedsManualReview);

        List<ReviewTaskResponse>? pendingReviews = await client.GetFromJsonAsync<List<ReviewTaskResponse>>("/api/v1/reviews", TestJsonOptions);
        ReviewTaskResponse failedReview = pendingReviews!.Single(r => r.JobId == invalidJobId);

        HttpResponseMessage approveInvalid = await client.PostAsJsonAsync(
            $"/api/v1/reviews/{failedReview.ReviewId}/approve",
            new ApproveReviewRequest("Trying to approve invalid translation"));
        approveInvalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // No CMS draft created when validation fails
        localCms.GetSavedDrafts(contentKey, "es-ES").Should().BeEmpty();
    }

    [Fact]
    public async Task WebhookIngestion_DuplicateExternalEventId_DeduplicatesWithoutDuplicateJob()
    {
        const string webhookSecret = "webhook-secret-xyz";
        (TenantId tenantId, ConnectionId connectionId) = await _factory.SeedTenantAndConnectionAsync(
            $"wh-{Guid.NewGuid():N}",
            webhookSecret);

        HttpClient client = _factory.CreateClient();
        var payloadObj = new OptimizelyWebhookRequest(
            Provider: "OptimizelySaaS",
            ExternalEventId: $"evt-{Guid.NewGuid():N}",
            ConnectionId: connectionId.Value,
            ExternalContentKey: "hotel-home-001",
            TargetLocale: "fr-FR",
            SourceLocale: "en");

        string rawJson = JsonSerializer.Serialize(payloadObj);
        string signature = "sha256=" + HmacWebhookSignatureVerifier.ComputeHmacSha256Hex(rawJson, webhookSecret);

        using var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/optimizely");
        req1.Headers.Add("X-OptiFlow-Tenant-Id", tenantId.Value.ToString());
        req1.Headers.Add("X-OptiFlow-Signature", signature);
        req1.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        HttpResponseMessage res1 = await client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        WebhookIngestResponse? body1 = await res1.Content.ReadFromJsonAsync<WebhookIngestResponse>();
        body1!.Deduplicated.Should().BeFalse();
        body1.CreatedJobId.Should().NotBeNull();

        using var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/optimizely");
        req2.Headers.Add("X-OptiFlow-Tenant-Id", tenantId.Value.ToString());
        req2.Headers.Add("X-OptiFlow-Signature", signature);
        req2.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        HttpResponseMessage res2 = await client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        WebhookIngestResponse? body2 = await res2.Content.ReadFromJsonAsync<WebhookIngestResponse>();
        body2!.Deduplicated.Should().BeTrue();
        body2.CreatedJobId.Should().BeNull();
    }

    [Fact]
    public async Task ReviewApprove_WhenCmsVersionChanged_Returns409StaleConflictAndSyncsLatest()
    {
        _factory.SpyLlm.Reset();
        (TenantId tenantId, ConnectionId connectionId) = await _factory.SeedTenantAndConnectionAsync($"stale-{Guid.NewGuid():N}");
        HttpClient client = _factory.CreateAuthenticatedClient(tenantId.Value);

        string contentKey = $"stale-page-{Guid.NewGuid():N}";
        LocalTestContentSource localCms = _factory.Services.GetRequiredService<LocalTestContentSource>();
        localCms.SeedSnapshot(new ContentSnapshot(
            contentKey, "v1-etag", "en", 1, "Article", "{}", [
                new ContentBlockSnapshot("title", "title", "heading", "Hello world", 0)
            ]));

        HttpResponseMessage syncRes = await client.PostAsJsonAsync(
            "/api/v1/content/sync",
            new SyncContentRequest(connectionId.Value, contentKey, "en"));
        SyncContentResponse? synced = await syncRes.Content.ReadFromJsonAsync<SyncContentResponse>();

        HttpResponseMessage jobRes = await client.PostAsJsonAsync(
            "/api/v1/localization/jobs",
            new CreateJobRequest(synced!.ContentItemId, synced.VersionId, ["de-DE"], $"stale-job-{Guid.NewGuid():N}"));
        CreateJobResponse? created = await jobRes.Content.ReadFromJsonAsync<CreateJobResponse>(TestJsonOptions);
        Guid jobId = created!.Jobs.Single().JobId;

        using (IServiceScope workerScope = _factory.Services.CreateScope())
        {
            ILocalizationOrchestrator orchestrator = workerScope.ServiceProvider.GetRequiredService<ILocalizationOrchestrator>();
            await orchestrator.ProcessAsync(jobId, CancellationToken.None);
        }

        // Advance CMS version while job is awaiting review!
        localCms.SeedSnapshot(new ContentSnapshot(
            contentKey, "v2-etag-newer", "en", 2, "Article", "{}", [
                new ContentBlockSnapshot("title", "title", "heading", "Hello updated world", 0)
            ]));

        List<ReviewTaskResponse>? reviews = await client.GetFromJsonAsync<List<ReviewTaskResponse>>("/api/v1/reviews", TestJsonOptions);
        ReviewTaskResponse review = reviews!.Single(r => r.JobId == jobId);

        HttpResponseMessage approveRes = await client.PostAsJsonAsync(
            $"/api/v1/reviews/{review.ReviewId}/approve",
            new ApproveReviewRequest("Approve"));

        approveRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
        ApiError? error = await approveRes.Content.ReadFromJsonAsync<ApiError>(TestJsonOptions);
        error!.Code.Should().Be("STALE_CONFLICT");

        JobStatusResponse? jobAfterConflict = await client.GetFromJsonAsync<JobStatusResponse>($"/api/v1/localization/jobs/{jobId}", TestJsonOptions);
        jobAfterConflict!.Status.Should().Be(JobStatus.Diffing);
        localCms.GetSavedDrafts(contentKey, "de-DE").Should().BeEmpty();
    }

    [Fact]
    public async Task TenantIsolation_CrossTenantJobRead_Returns404NotFound()
    {
        (TenantId tenantA, ConnectionId connA) = await _factory.SeedTenantAndConnectionAsync($"ta-{Guid.NewGuid():N}");
        (TenantId tenantB, _) = await _factory.SeedTenantAndConnectionAsync($"tb-{Guid.NewGuid():N}");

        HttpClient clientA = _factory.CreateAuthenticatedClient(tenantA.Value, "user-a");
        HttpClient clientB = _factory.CreateAuthenticatedClient(tenantB.Value, "user-b");

        HttpResponseMessage syncRes = await clientA.PostAsJsonAsync(
            "/api/v1/content/sync",
            new SyncContentRequest(connA.Value, "hotel-home-001", "en"));
        SyncContentResponse? synced = await syncRes.Content.ReadFromJsonAsync<SyncContentResponse>(TestJsonOptions);

        HttpResponseMessage jobRes = await clientA.PostAsJsonAsync(
            "/api/v1/localization/jobs",
            new CreateJobRequest(synced!.ContentItemId, synced.VersionId, ["es-ES"], $"iso-{Guid.NewGuid():N}"));
        Guid jobId = (await jobRes.Content.ReadFromJsonAsync<CreateJobResponse>(TestJsonOptions))!.Jobs.Single().JobId;

        HttpResponseMessage crossTenantGet = await clientB.GetAsync($"/api/v1/localization/jobs/{jobId}");
        crossTenantGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EndToEndHappyPath_ApprovePromotesTmAndSubsequentVersionReusesApprovedMemoryWithAnalytics()
    {
        _factory.SpyLlm.Reset();
        (TenantId tenantId, ConnectionId connectionId) = await _factory.SeedTenantAndConnectionAsync($"e2e-{Guid.NewGuid():N}");
        HttpClient client = _factory.CreateAuthenticatedClient(tenantId.Value);

        string contentKey = $"e2e-page-{Guid.NewGuid():N}";
        LocalTestContentSource localCms = _factory.Services.GetRequiredService<LocalTestContentSource>();
        localCms.SeedSnapshot(new ContentSnapshot(
            contentKey, "v1", "en", 1, "LandingPage", "{}", [
                new ContentBlockSnapshot("hero", "hero.title", "heading", "Luxury suite", 0)
            ]));

        // Re-sync same version twice -> immutable snapshot (second returns IsNewVersion=false)
        SyncContentResponse? s1 = await (await client.PostAsJsonAsync("/api/v1/content/sync", new SyncContentRequest(connectionId.Value, contentKey, "en")))
            .Content.ReadFromJsonAsync<SyncContentResponse>(TestJsonOptions);
        SyncContentResponse? s2 = await (await client.PostAsJsonAsync("/api/v1/content/sync", new SyncContentRequest(connectionId.Value, contentKey, "en")))
            .Content.ReadFromJsonAsync<SyncContentResponse>(TestJsonOptions);
        s1!.IsNewVersion.Should().BeTrue();
        s2!.IsNewVersion.Should().BeFalse();
        s2.VersionId.Should().Be(s1.VersionId);

        Guid job1 = (await (await client.PostAsJsonAsync("/api/v1/localization/jobs",
            new CreateJobRequest(s1.ContentItemId, s1.VersionId, ["es-ES"], $"e2e-j1-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<CreateJobResponse>(TestJsonOptions))!.Jobs.Single().JobId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ILocalizationOrchestrator>().ProcessAsync(job1, CancellationToken.None);
        }
        _factory.SpyLlm.CallCount.Should().Be(1);

        ReviewTaskResponse review = (await client.GetFromJsonAsync<List<ReviewTaskResponse>>("/api/v1/reviews", TestJsonOptions))!.Single(r => r.JobId == job1);
        HttpResponseMessage approveRes = await client.PostAsJsonAsync(
            $"/api/v1/reviews/{review.ReviewId}/approve",
            new ApproveReviewRequest("Approved by human", [new ReviewBlockOverride("hero", "Suite de lujo")]));
        approveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        ReviewDecisionResponse? decision = await approveRes.Content.ReadFromJsonAsync<ReviewDecisionResponse>(TestJsonOptions);
        decision!.JobStatus.Should().Be(JobStatus.CmsDraftCreated);
        decision.CmsDraftId.Should().NotBeNullOrWhiteSpace();
        localCms.GetSavedDrafts(contentKey, "es-ES").Should().HaveCount(1);

        // Analytics check
        CostAnalyticsResponse? costs = await client.GetFromJsonAsync<CostAnalyticsResponse>("/api/v1/analytics/costs", TestJsonOptions);
        PerformanceAnalyticsResponse? perf = await client.GetFromJsonAsync<PerformanceAnalyticsResponse>("/api/v1/analytics/performance", TestJsonOptions);
        costs!.TotalJobs.Should().Be(1);
        perf!.SucceededJobs.Should().Be(1);
    }

    [Fact]
    public async Task WorkerQueue_ExpiredLeaseAndTransientFailures_ReclaimsAndMovesToDeadLetter()
    {
        _factory.SpyLlm.Reset();
        _factory.SpyLlm.ConfigureResponder(_ => new TranslationResult(
            IsSuccess: false, RawJson: null, TranslatedText: null, Confidence: null,
            InputTokens: 0, OutputTokens: 0, LatencyMs: 5, ModelName: "gemma-4b", Provider: "Spy",
            Status: AiInvocationStatus.TimedOut, IsTransientError: true, ErrorCode: "AI_TIMEOUT"));

        (TenantId tenantId, ConnectionId connectionId) = await _factory.SeedTenantAndConnectionAsync($"lease-{Guid.NewGuid():N}");
        HttpClient client = _factory.CreateAuthenticatedClient(tenantId.Value);

        SyncContentResponse? synced = await (await client.PostAsJsonAsync(
            "/api/v1/content/sync",
            new SyncContentRequest(connectionId.Value, "hotel-home-001", "en")))
            .Content.ReadFromJsonAsync<SyncContentResponse>(TestJsonOptions);

        Guid jobId = (await (await client.PostAsJsonAsync("/api/v1/localization/jobs",
            new CreateJobRequest(synced!.ContentItemId, synced.VersionId, ["it-IT"], $"dl-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<CreateJobResponse>(TestJsonOptions))!.Jobs.Single().JobId;

        // 1. Verify active lease is NOT stolen, while expired lease IS reclaimed
        using (IServiceScope seedScope = _factory.Services.CreateScope())
        {
            OptiFlowDbContext db = seedScope.ServiceProvider.GetRequiredService<OptiFlowDbContext>();
            IJobQueue queue = seedScope.ServiceProvider.GetRequiredService<IJobQueue>();
            LocalizationJob? firstClaim = await queue.ClaimNextAsync("crashed-worker", CancellationToken.None);
            firstClaim.Should().NotBeNull();

            // Active lease -> another worker gets null
            (await queue.ClaimNextAsync("healthy-worker", CancellationToken.None)).Should().BeNull();

            // Simulate lease expiry after worker crash
            DateTimeOffset past = DateTimeOffset.UtcNow.AddMinutes(-10);
            await db.LocalizationJobs
                .IgnoreQueryFilters()
                .Where(j => j.Id == new JobId(jobId))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.LeaseExpiresAt, past)
                    .SetProperty(j => j.AttemptCount, 0));
        }

        // 2. Simulate 4 transient failures (maxAttempts=4) across fresh worker scopes -> DeadLetter
        for (int attempt = 1; attempt <= 4; attempt++)
        {
            using IServiceScope workerScope = _factory.Services.CreateScope();
            IJobQueue queue = workerScope.ServiceProvider.GetRequiredService<IJobQueue>();
            ILocalizationOrchestrator orchestrator = workerScope.ServiceProvider.GetRequiredService<ILocalizationOrchestrator>();

            LocalizationJob? claimed = await queue.ClaimNextAsync($"worker-{attempt}", CancellationToken.None);
            claimed.Should().NotBeNull();
            await orchestrator.ProcessAsync(claimed!.Id.Value, CancellationToken.None);

            if (attempt < 4)
            {
                OptiFlowDbContext db = workerScope.ServiceProvider.GetRequiredService<OptiFlowDbContext>();
                DateTimeOffset past = DateTimeOffset.UtcNow.AddMinutes(-1);
                await db.LocalizationJobs
                    .IgnoreQueryFilters()
                    .Where(j => j.Id == new JobId(jobId))
                    .ExecuteUpdateAsync(s => s.SetProperty(j => j.AvailableAt, past));
            }
        }

        JobStatusResponse? finalJob = await client.GetFromJsonAsync<JobStatusResponse>($"/api/v1/localization/jobs/{jobId}", TestJsonOptions);
        finalJob!.Status.Should().Be(JobStatus.DeadLetter);
        finalJob.AttemptCount.Should().Be(4);
    }
}
