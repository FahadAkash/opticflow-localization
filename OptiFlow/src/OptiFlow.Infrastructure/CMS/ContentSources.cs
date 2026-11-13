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

        var frenchBlocks = new List<ContentBlockSnapshot>
        {
            new("hero-title", "sections[0].title", "heading", "Bienvenue dans notre hôtel de luxe à Paris", 0),
            new("hero-cta", "sections[0].ctaLabel", "text", "Réservez votre séjour dès aujourd'hui", 1),
            new("promo-body", "sections[1].body", "richtext", "<p>Profitez du <strong>petit-déjeuner offert</strong> pendant votre séjour.</p>", 2)
        };

        SeedSnapshot(new ContentSnapshot(
            ExternalContentKey: "hotel-paris-001",
            ExternalVersionKey: "v1-etag-paris-001",
            Locale: "fr-FR",
            VersionNumber: 1,
            ContentType: "LandingPage",
            RawPayloadJson: JsonSerializer.Serialize(new { key = "hotel-paris-001", version = "v1-etag-paris-001", blocks = frenchBlocks }),
            Blocks: frenchBlocks));

        var roomsBlocks = new List<ContentBlockSnapshot>
        {
            new("rooms-hero-title", "header.title", "heading", "Luxury Suites & Oceanfront Villas", 0),
            new("rooms-hero-desc", "header.description", "text", "Immerse yourself in panoramic Mediterranean views, private infinity plunge pools, and bespoke Italian marble interiors.", 1),
            new("suite-azure-title", "suites[0].name", "heading", "The Presidential Azure Penthouse", 2),
            new("suite-azure-desc", "suites[0].description", "richtext", "<p>Features a <strong>private rooftop terrace</strong>, infinity whirlpool, and personal sommelier service.</p>", 3),
            new("suite-villa-title", "suites[1].name", "heading", "Riviera Royal Garden Villa", 4),
            new("suite-villa-desc", "suites[1].description", "richtext", "<p>Direct beach access with private botanical garden and <strong>dedicated 24/7 butler</strong>.</p>", 5),
            new("rooms-booking-cta", "footer.ctaLabel", "text", "Check Suite Availability", 6)
        };

        SeedSnapshot(new ContentSnapshot(
            ExternalContentKey: "hotel-rooms-002",
            ExternalVersionKey: "v1-etag-rooms-001",
            Locale: "en",
            VersionNumber: 1,
            ContentType: "RoomsPage",
            RawPayloadJson: JsonSerializer.Serialize(new { key = "hotel-rooms-002", version = "v1-etag-rooms-001", blocks = roomsBlocks }),
            Blocks: roomsBlocks));

        var diningBlocks = new List<ContentBlockSnapshot>
        {
            new("dining-hero-title", "header.title", "heading", "Michelin Gastronomy & Azure Wellness Spa", 0),
            new("dining-hero-desc", "header.description", "text", "An extraordinary sensory voyage crafted by world-renowned chefs and ancient holistic thalassotherapy rituals.", 1),
            new("restaurant-title", "dining.restaurantName", "heading", "Le Mirador Oceanfront Restaurant", 2),
            new("restaurant-desc", "dining.description", "richtext", "<p>Three-star Michelin culinary artistry highlighting <strong>wild Mediterranean seafood</strong> and organic harvest.</p>", 3),
            new("spa-title", "spa.facilityName", "heading", "Azure Thalasso & Thermal Sanctuary", 4),
            new("spa-desc", "spa.description", "richtext", "<p>Rejuvenating seawater hydrotherapy, eucalyptus steam grottos, and <strong>custom botanical treatments</strong>.</p>", 5),
            new("dining-reserve-cta", "footer.ctaLabel", "text", "Reserve a Table or Treatment", 6)
        };

        SeedSnapshot(new ContentSnapshot(
            ExternalContentKey: "hotel-dining-003",
            ExternalVersionKey: "v1-etag-dining-001",
            Locale: "en",
            VersionNumber: 1,
            ContentType: "DiningAndSpaPage",
            RawPayloadJson: JsonSerializer.Serialize(new { key = "hotel-dining-003", version = "v1-etag-dining-001", blocks = diningBlocks }),
            Blocks: diningBlocks));
    }

    public void SimulateCmsUpdateV2(string contentKey = "hotel-home-001", string locale = "en")
    {
        var blocks = new List<ContentBlockSnapshot>
        {
            new("hero-title", "sections[0].title", "heading", "Welcome to our newly renovated luxury resort", 0),
            new("hero-cta", "sections[0].ctaLabel", "text", "Book your stay today", 1),
            new("promo-body", "sections[1].body", "richtext", "<p>Enjoy <strong>complimentary</strong> breakfast during your visit.</p>", 2),
            new("amenities-banner", "sections[2].banner", "text", "Complimentary spa access and valet parking included with all suites.", 3)
        };

        SeedSnapshot(new ContentSnapshot(
            ExternalContentKey: contentKey,
            ExternalVersionKey: "v2-etag-002",
            Locale: locale,
            VersionNumber: 2,
            ContentType: "LandingPage",
            RawPayloadJson: JsonSerializer.Serialize(new { key = contentKey, version = "v2-etag-002", blocks }),
            Blocks: blocks));
    }

    public void SimulateCmsRollback(string contentKey = "hotel-home-001", string locale = "en")
    {
        var blocks = new List<ContentBlockSnapshot>
        {
            new("hero-title", "sections[0].title", "heading", "Welcome to our luxury hotel", 0),
            new("hero-cta", "sections[0].ctaLabel", "text", "Book your stay today", 1),
            new("promo-body", "sections[1].body", "richtext", "<p>Enjoy <strong>complimentary</strong> breakfast during your visit.</p>", 2)
        };

        SeedSnapshot(new ContentSnapshot(
            ExternalContentKey: contentKey,
            ExternalVersionKey: "v3-etag-rollback",
            Locale: locale,
            VersionNumber: 3,
            ContentType: "LandingPage",
            RawPayloadJson: JsonSerializer.Serialize(new { key = contentKey, version = "v3-etag-rollback", blocks }),
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
