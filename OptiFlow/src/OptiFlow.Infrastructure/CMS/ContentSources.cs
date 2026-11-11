using System.Collections.Concurrent;
using System.Text.Json;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;

namespace OptiFlow.Infrastructure.CMS;

public sealed class LocalTestContentSource : IContentSource
{
    private readonly ConcurrentDictionary<string, List<ContentSnapshot>> _versionsByContentKey =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, List<DraftContent>> _draftsByKey =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly TimeProvider _timeProvider;

    public LocalTestContentSource(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        SeedDefaultSampleContent();
    }

    public void SeedSnapshot(ContentSnapshot snapshot)
    {
        string key = BuildKey(snapshot.ExternalContentKey, snapshot.Locale);
        List<ContentSnapshot> list = _versionsByContentKey.GetOrAdd(key, _ => []);
        lock (list)
        {
            list.Add(snapshot);
        }
    }

    public IReadOnlyList<DraftContent> GetSavedDrafts(string contentKey, string targetLocale)
    {
        string key = BuildKey(contentKey, targetLocale);
        if (_draftsByKey.TryGetValue(key, out List<DraftContent>? drafts))
        {
            lock (drafts)
            {
                return drafts.ToList();
            }
        }
        return [];
    }

    public Task<ContentSnapshot?> GetContentAsync(
        string contentKey,
        string locale,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = BuildKey(contentKey, locale);
        if (_versionsByContentKey.TryGetValue(key, out List<ContentSnapshot>? versions))
        {
            lock (versions)
            {
                return Task.FromResult<ContentSnapshot?>(versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault());
            }
        }
        return Task.FromResult<ContentSnapshot?>(null);
    }

    public Task<IReadOnlyList<ContentVersionSummary>> GetVersionsAsync(
        string contentKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<ContentVersionSummary>();
        foreach (KeyValuePair<string, List<ContentSnapshot>> entry in _versionsByContentKey)
        {
            if (!entry.Key.StartsWith(contentKey + "|", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            lock (entry.Value)
            {
                results.AddRange(entry.Value.Select(v =>
                    new ContentVersionSummary(v.ExternalVersionKey, v.VersionNumber, v.Locale, _timeProvider.GetUtcNow())));
            }
        }

        return Task.FromResult<IReadOnlyList<ContentVersionSummary>>(results.OrderBy(v => v.VersionNumber).ToList());
    }

    public Task<DraftResult> CreateDraftAsync(
        DraftContent content,
        string expectedVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Find latest source snapshot across locales for optimistic concurrency check
        ContentSnapshot? latest = _versionsByContentKey
            .Where(kvp => kvp.Key.StartsWith(content.ExternalContentKey + "|", StringComparison.OrdinalIgnoreCase))
            .SelectMany(kvp => kvp.Value)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        if (latest is not null && !string.Equals(latest.ExternalVersionKey, expectedVersion, StringComparison.Ordinal))
        {
            return Task.FromResult(new DraftResult(
                Succeeded: false,
                IsVersionMismatch: true,
                DraftId: null,
                NewVersionKey: latest.ExternalVersionKey,
                ErrorMessage: $"Expected version '{expectedVersion}', but CMS is at '{latest.ExternalVersionKey}'."));
        }

        string draftKey = BuildKey(content.ExternalContentKey, content.TargetLocale);
        List<DraftContent> drafts = _draftsByKey.GetOrAdd(draftKey, _ => []);
        lock (drafts)
        {
            drafts.Add(content);
        }

        string draftId = $"draft-{Guid.NewGuid():N}";
        return Task.FromResult(new DraftResult(
            Succeeded: true,
            IsVersionMismatch: false,
            DraftId: draftId,
            NewVersionKey: expectedVersion));
    }

    private void SeedDefaultSampleContent()
    {
        var blocks = new List<ContentBlockSnapshot>
        {
            new("hero-title", "sections[0].title", "heading", "Welcome to our luxury hotel", 0),
            new("hero-cta", "sections[0].ctaLabel", "text", "Book your stay today", 1),
            new("promo-body", "sections[1].body", "richtext", "<p>Enjoy <strong>complimentary</strong> breakfast during your visit.</p>", 2)
        };

        SeedSnapshot(new ContentSnapshot(
            ExternalContentKey: "hotel-home-001",
            ExternalVersionKey: "v1-etag-001",
            Locale: "en",
            VersionNumber: 1,
            ContentType: "LandingPage",
            RawPayloadJson: JsonSerializer.Serialize(new { key = "hotel-home-001", version = "v1-etag-001", blocks }),
            Blocks: blocks));
    }

    private static string BuildKey(string contentKey, string locale) => $"{contentKey}|{locale}";
}

public sealed class OptimizelySaaSContentSource : IContentSource
{
    private readonly HttpClient _httpClient;

    public OptimizelySaaSContentSource(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ContentSnapshot?> GetContentAsync(
        string contentKey,
        string locale,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        using HttpResponseMessage response = await _httpClient
            .GetAsync($"content/{Uri.EscapeDataString(contentKey)}?locale={Uri.EscapeDataString(locale)}", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        string rawJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        OptimizelyContentPayload? payload = JsonSerializer.Deserialize<OptimizelyContentPayload>(rawJson);

        if (payload is null || payload.Blocks is null)
        {
            throw new InvalidOperationException("CMS response payload failed schema validation.");
        }

        List<ContentBlockSnapshot> blocks = payload.Blocks
            .Select((b, idx) => new ContentBlockSnapshot(b.BlockKey, b.FieldPath, b.BlockType, b.SourceText, idx))
            .ToList();

        return new ContentSnapshot(
            payload.ContentKey,
            payload.VersionKey,
            payload.Locale,
            payload.VersionNumber,
            payload.ContentType,
            rawJson,
            blocks);
    }

    public async Task<IReadOnlyList<ContentVersionSummary>> GetVersionsAsync(
        string contentKey,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .GetAsync($"content/{Uri.EscapeDataString(contentKey)}/versions", cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        string rawJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        List<ContentVersionSummary>? versions = JsonSerializer.Deserialize<List<ContentVersionSummary>>(rawJson);
        return versions ?? [];
    }

    public async Task<DraftResult> CreateDraftAsync(
        DraftContent content,
        string expectedVersion,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"content/{Uri.EscapeDataString(content.ExternalContentKey)}/drafts");
        request.Headers.TryAddWithoutValidation("If-Match", expectedVersion);
        request.Content = new StringContent(
            JsonSerializer.Serialize(content),
            System.Text.Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.PreconditionFailed or System.Net.HttpStatusCode.Conflict)
        {
            return new DraftResult(
                Succeeded: false,
                IsVersionMismatch: true,
                DraftId: null,
                NewVersionKey: null,
                ErrorMessage: "CMS version mismatch (ETag precondition failed).");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new DraftResult(
                Succeeded: false,
                IsVersionMismatch: false,
                DraftId: null,
                NewVersionKey: null,
                ErrorMessage: $"CMS draft endpoint returned {(int)response.StatusCode}.");
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        OptimizelyDraftResponse? parsed = JsonSerializer.Deserialize<OptimizelyDraftResponse>(body);

        return new DraftResult(
            Succeeded: true,
            IsVersionMismatch: false,
            DraftId: parsed?.DraftId ?? $"cms-draft-{Guid.NewGuid():N}",
            NewVersionKey: parsed?.VersionKey ?? expectedVersion);
    }

    private sealed record OptimizelyContentPayload(
        string ContentKey,
        string VersionKey,
        string Locale,
        int VersionNumber,
        string ContentType,
        List<OptimizelyBlockPayload>? Blocks);

    private sealed record OptimizelyBlockPayload(
        string BlockKey,
        string FieldPath,
        string BlockType,
        string SourceText);

    private sealed record OptimizelyDraftResponse(string? DraftId, string? VersionKey);
}

public sealed class ContentSourceResolver : IContentSourceResolver
{
    private readonly LocalTestContentSource _localSource;
    private readonly OptimizelySaaSContentSource _saasSource;

    public ContentSourceResolver(
        LocalTestContentSource localSource,
        OptimizelySaaSContentSource saasSource)
    {
        _localSource = localSource;
        _saasSource = saasSource;
    }

    public IContentSource Resolve(CmsProvider provider) =>
        provider switch
        {
            CmsProvider.LocalTest => _localSource,
            CmsProvider.OptimizelySaaS => _saasSource,
            _ => throw new NotSupportedException($"CMS provider {provider} is not implemented yet.")
        };
}
