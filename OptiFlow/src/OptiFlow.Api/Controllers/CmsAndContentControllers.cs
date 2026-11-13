using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OptiFlow.Application.Abstractions;
using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Common;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

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
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ContentController(
        IContentSyncService syncService,
        IOptiFlowRepository repository,
        ITenantContext tenantContext,
        IValidator<SyncContentRequest> syncValidator,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _syncService = syncService;
        _repository = repository;
        _tenantContext = tenantContext;
        _syncValidator = syncValidator;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
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
        var sampleKeys = new[] { "hotel-home-001", "hotel-rooms-002", "hotel-dining-003" };
        if (sampleKeys.Any(k => !items.Any(i => i.ExternalContentKey.Value == k)))
        {
            var connections = await _repository.ListConnectionsAsync(_tenantContext.CurrentTenantId, cancellationToken);
            var connection = connections.FirstOrDefault();
            if (connection is not null)
            {
                foreach (string key in sampleKeys)
                {
                    if (!items.Any(i => i.ExternalContentKey.Value == key))
                    {
                        await _syncService.SyncAsync(new SyncContentRequest(connection.Id.Value, key, "en"), cancellationToken);
                    }
                }
                items = await _repository.ListContentItemsAsync(_tenantContext.CurrentTenantId, cancellationToken);
            }
        }

        foreach (ContentItem item in items)
        {
            await EnsureSampleTranslationsAsync(_tenantContext.CurrentTenantId, item, cancellationToken);
        }

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

    private async Task EnsureSampleTranslationsAsync(TenantId tenantId, ContentItem item, CancellationToken cancellationToken)
    {
        IReadOnlyList<LocaleCode> availableLocales = await _repository
            .GetAvailableTranslationLocalesAsync(tenantId, item.Id, cancellationToken);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        bool anyAdded = false;

        if (item.ExternalContentKey.Value == "hotel-rooms-002")
        {
            if (!availableLocales.Any(l => l.Value.Equals("BD", StringComparison.OrdinalIgnoreCase)))
            {
                var bdTranslations = new (string Key, string Source, string Trans)[]
                {
                    ("rooms-hero-title", "Luxury Suites & Oceanfront Villas", "বিলাসবহুল স্যুট এবং সমুদ্রমুখী ভিলা"),
                    ("rooms-hero-desc", "Immerse yourself in panoramic Mediterranean views, private infinity plunge pools, and bespoke Italian marble interiors.", "প্যানোরামিক ভূমধ্যসাগরীয় দৃশ্য, ব্যক্তিগত ইনফিনিটি প্লাঞ্জ পুল এবং ইতালিয়ান মার্বেল ইন্টেরিয়র উপভোগ করুন।"),
                    ("suite-azure-title", "The Presidential Azure Penthouse", "প্রেসিডেন্সিয়াল অ্যাজুর পেন্টহাউস"),
                    ("suite-azure-desc", "<p>Features a <strong>private rooftop terrace</strong>, infinity whirlpool, and personal sommelier service.</p>", "<p>রয়েছে একটি <strong>ব্যক্তিগত রুফটপ টেরেস</strong>, ইনফিনিটি ঘূর্ণিপুল এবং ব্যক্তিগত সোমেলিয়ার সেবা।</p>"),
                    ("suite-villa-title", "Riviera Royal Garden Villa", "রিভিয়েরা রয়্যাল গার্ডেন ভিলা"),
                    ("suite-villa-desc", "<p>Direct beach access with private botanical garden and <strong>dedicated 24/7 butler</strong>.</p>", "<p>ব্যক্তিগত বোটানিক্যাল গার্ডেন এবং <strong>২৪/৭ নিবেদিত বাটলার</strong> সহ সরাসরি সৈকত সুবিধা।</p>"),
                    ("rooms-booking-cta", "Check Suite Availability", "স্যুটের প্রাপ্যতা যাচাই করুন")
                };

                foreach (var t in bdTranslations)
                {
                    await _repository.UpsertTranslationAsync(new Translation(
                        TranslationId.New(), tenantId, item.Id, new BlockKey(t.Key), new LocaleCode("BD"),
                        ContentHash.FromRawText(t.Source), t.Trans, TranslationProvenance.HumanApproved, now), cancellationToken);
                }
                anyAdded = true;
            }

            if (!availableLocales.Any(l => l.Value.Equals("es-ES", StringComparison.OrdinalIgnoreCase)))
            {
                var esTranslations = new (string Key, string Source, string Trans)[]
                {
                    ("rooms-hero-title", "Luxury Suites & Oceanfront Villas", "Suites de Lujo y Villas Frente al Mar"),
                    ("rooms-hero-desc", "Immerse yourself in panoramic Mediterranean views, private infinity plunge pools, and bespoke Italian marble interiors.", "Sumérjase en vistas panorámicas al Mediterráneo, piscinas privadas de borde infinito y mármol italiano."),
                    ("suite-azure-title", "The Presidential Azure Penthouse", "El Ático Presidencial Azure"),
                    ("suite-azure-desc", "<p>Features a <strong>private rooftop terrace</strong>, infinity whirlpool, and personal sommelier service.</p>", "<p>Cuenta con <strong>terraza privada en la azotea</strong>, hidromasaje infinito y servicio de sumiller personal.</p>"),
                    ("suite-villa-title", "Riviera Royal Garden Villa", "Villa Riviera Royal Garden"),
                    ("suite-villa-desc", "<p>Direct beach access with private botanical garden and <strong>dedicated 24/7 butler</strong>.</p>", "<p>Acceso directo a la playa con jardín botánico privado y <strong>mayordomo dedicado 24/7</strong>.</p>"),
                    ("rooms-booking-cta", "Check Suite Availability", "Consultar Disponibilidad de Suites")
                };

                foreach (var t in esTranslations)
                {
                    await _repository.UpsertTranslationAsync(new Translation(
                        TranslationId.New(), tenantId, item.Id, new BlockKey(t.Key), new LocaleCode("es-ES"),
                        ContentHash.FromRawText(t.Source), t.Trans, TranslationProvenance.HumanApproved, now), cancellationToken);
                }
                anyAdded = true;
            }

            if (!availableLocales.Any(l => l.Value.Equals("fr-FR", StringComparison.OrdinalIgnoreCase)))
            {
                var frTranslations = new (string Key, string Source, string Trans)[]
                {
                    ("rooms-hero-title", "Luxury Suites & Oceanfront Villas", "Suites de Prestige & Villas Front de Mer"),
                    ("rooms-hero-desc", "Immerse yourself in panoramic Mediterranean views, private infinity plunge pools, and bespoke Italian marble interiors.", "Plongez dans des panoramas méditerranéens, piscines à débordement privées et intérieurs en marbre italien."),
                    ("suite-azure-title", "The Presidential Azure Penthouse", "Le Penthouse Présidentiel Azure"),
                    ("suite-azure-desc", "<p>Features a <strong>private rooftop terrace</strong>, infinity whirlpool, and personal sommelier service.</p>", "<p>Comprend une <strong>terrasse privée sur le toit</strong>, un bain à remous infini et un sommelier dédié.</p>"),
                    ("suite-villa-title", "Riviera Royal Garden Villa", "Villa Riviera Royal Garden"),
                    ("suite-villa-desc", "<p>Direct beach access with private botanical garden and <strong>dedicated 24/7 butler</strong>.</p>", "<p>Accès direct à la plage avec jardin botanique privatif et <strong>majordome dédié 24h/24</strong>.</p>"),
                    ("rooms-booking-cta", "Check Suite Availability", "Vérifier la Disponibilité des Suites")
                };

                foreach (var t in frTranslations)
                {
                    await _repository.UpsertTranslationAsync(new Translation(
                        TranslationId.New(), tenantId, item.Id, new BlockKey(t.Key), new LocaleCode("fr-FR"),
                        ContentHash.FromRawText(t.Source), t.Trans, TranslationProvenance.HumanApproved, now), cancellationToken);
                }
                anyAdded = true;
            }
        }
        else if (item.ExternalContentKey.Value == "hotel-dining-003")
        {
            if (!availableLocales.Any(l => l.Value.Equals("BD", StringComparison.OrdinalIgnoreCase)))
            {
                var bdTranslations = new (string Key, string Source, string Trans)[]
                {
                    ("dining-hero-title", "Michelin Gastronomy & Azure Wellness Spa", "মিশেলিন গ্যাস্ট্রোনমি এবং অ্যাজুর ওয়েলনেস স্পা"),
                    ("dining-hero-desc", "An extraordinary sensory voyage crafted by world-renowned chefs and ancient holistic thalassotherapy rituals.", "বিশ্বখ্যাত শেফ এবং প্রাচীন সামগ্রিক থ্যালাসোথেরাপির অনন্য সংমিশ্রণে এক অসাধারণ অভিজ্ঞতা।"),
                    ("restaurant-title", "Le Mirador Oceanfront Restaurant", "লা মিরাডোর ওশানফ্রন্ট রেস্তোরাঁ"),
                    ("restaurant-desc", "<p>Three-star Michelin culinary artistry highlighting <strong>wild Mediterranean seafood</strong> and organic harvest.</p>", "<p>তিন তারকা মিশেলিন রন্ধনশিল্প যেখানে রয়েছে <strong>তাজা ভূমধ্যসাগরীয় সামুদ্রিক খাবার</strong> এবং অর্গানিক উপাদান।</p>"),
                    ("spa-title", "Azure Thalasso & Thermal Sanctuary", "অ্যাজুর থ্যালাসো এবং থার্মাল অভয়ারণ্য"),
                    ("spa-desc", "<p>Rejuvenating seawater hydrotherapy, eucalyptus steam grottos, and <strong>custom botanical treatments</strong>.</p>", "<p>পুনরুজ্জীবিত সমুদ্রের জলের হাইড্রোথেরাপি, ইউক্যালিপটাস স্টিম গ্রোটো এবং <strong>বিশেষ বোটানিক্যাল ট্রিটমেন্ট</strong>।</p>"),
                    ("dining-reserve-cta", "Reserve a Table or Treatment", "টেবিল বা ট্রিটমেন্ট বুক করুন")
                };

                foreach (var t in bdTranslations)
                {
                    await _repository.UpsertTranslationAsync(new Translation(
                        TranslationId.New(), tenantId, item.Id, new BlockKey(t.Key), new LocaleCode("BD"),
                        ContentHash.FromRawText(t.Source), t.Trans, TranslationProvenance.HumanApproved, now), cancellationToken);
                }
                anyAdded = true;
            }

            if (!availableLocales.Any(l => l.Value.Equals("es-ES", StringComparison.OrdinalIgnoreCase)))
            {
                var esTranslations = new (string Key, string Source, string Trans)[]
                {
                    ("dining-hero-title", "Michelin Gastronomy & Azure Wellness Spa", "Gastronomía Michelin y Azure Wellness Spa"),
                    ("dining-hero-desc", "An extraordinary sensory voyage crafted by world-renowned chefs and ancient holistic thalassotherapy rituals.", "Un extraordinario viaje sensorial creado por chefs de renombre internacional y rituales de talasoterapia."),
                    ("restaurant-title", "Le Mirador Oceanfront Restaurant", "Restaurante Frente al Mar Le Mirador"),
                    ("restaurant-desc", "<p>Three-star Michelin culinary artistry highlighting <strong>wild Mediterranean seafood</strong> and organic harvest.</p>", "<p>Alta cocina con 3 estrellas Michelin destacando <strong>mariscos frescos del Mediterráneo</strong> y huerto orgánico.</p>"),
                    ("spa-title", "Azure Thalasso & Thermal Sanctuary", "Santuario Termal y Talasoterapia Azure"),
                    ("spa-desc", "<p>Rejuvenating seawater hydrotherapy, eucalyptus steam grottos, and <strong>custom botanical treatments</strong>.</p>", "<p>Hidroterapia revitalizante marina, grutas aromáticas de eucalipto y <strong>tratamientos botánicos exclusivos</strong>.</p>"),
                    ("dining-reserve-cta", "Reserve a Table or Treatment", "Reservar Mesa o Tratamiento")
                };

                foreach (var t in esTranslations)
                {
                    await _repository.UpsertTranslationAsync(new Translation(
                        TranslationId.New(), tenantId, item.Id, new BlockKey(t.Key), new LocaleCode("es-ES"),
                        ContentHash.FromRawText(t.Source), t.Trans, TranslationProvenance.HumanApproved, now), cancellationToken);
                }
                anyAdded = true;
            }

            if (!availableLocales.Any(l => l.Value.Equals("fr-FR", StringComparison.OrdinalIgnoreCase)))
            {
                var frTranslations = new (string Key, string Source, string Trans)[]
                {
                    ("dining-hero-title", "Michelin Gastronomy & Azure Wellness Spa", "Gastronomie Étoilée Michelin & Azure Wellness Spa"),
                    ("dining-hero-desc", "An extraordinary sensory voyage crafted by world-renowned chefs and ancient holistic thalassotherapy rituals.", "Un voyage sensoriel d'exception orchestré par des chefs renommés et des rituels de thalassothérapie."),
                    ("restaurant-title", "Le Mirador Oceanfront Restaurant", "Restaurant Panoramique Le Mirador"),
                    ("restaurant-desc", "<p>Three-star Michelin culinary artistry highlighting <strong>wild Mediterranean seafood</strong> and organic harvest.</p>", "<p>Art culinaire 3 étoiles Michelin mettant à l'honneur les <strong>fruits de mer sauvages de Méditerranée</strong>.</p>"),
                    ("spa-title", "Azure Thalasso & Thermal Sanctuary", "Sanctuaire Thalasso & Thermal Azure"),
                    ("spa-desc", "<p>Rejuvenating seawater hydrotherapy, eucalyptus steam grottos, and <strong>custom botanical treatments</strong>.</p>", "<p>Hydrothérapie vivifiante à l'eau de mer, grottes de vapeur d'eucalyptus et <strong>soins botaniques sur mesure</strong>.</p>"),
                    ("dining-reserve-cta", "Reserve a Table or Treatment", "Réserver une Table ou un Soin")
                };

                foreach (var t in frTranslations)
                {
                    await _repository.UpsertTranslationAsync(new Translation(
                        TranslationId.New(), tenantId, item.Id, new BlockKey(t.Key), new LocaleCode("fr-FR"),
                        ContentHash.FromRawText(t.Source), t.Trans, TranslationProvenance.HumanApproved, now), cancellationToken);
                }
                anyAdded = true;
            }
        }
        else if (item.ExternalContentKey.Value == "hotel-home-001")
        {
            if (!availableLocales.Any(l => l.Value.Equals("es-ES", StringComparison.OrdinalIgnoreCase)))
            {
                var esHome = new (string Key, string Source, string Trans)[]
                {
                    ("hero-title", "Welcome to our luxury hotel", "Bienvenido a nuestro hotel de lujo"),
                    ("hero-cta", "Book your stay today", "Reserve su estancia hoy"),
                    ("promo-body", "<p>Enjoy <strong>complimentary</strong> breakfast during your visit.</p>", "<p>Disfrute de desayuno <strong>de cortesía</strong> durante su visita.</p>")
                };

                foreach (var t in esHome)
                {
                    await _repository.UpsertTranslationAsync(new Translation(
                        TranslationId.New(), tenantId, item.Id, new BlockKey(t.Key), new LocaleCode("es-ES"),
                        ContentHash.FromRawText(t.Source), t.Trans, TranslationProvenance.HumanApproved, now), cancellationToken);
                }
                anyAdded = true;
            }

            if (!availableLocales.Any(l => l.Value.Equals("fr-FR", StringComparison.OrdinalIgnoreCase)))
            {
                var frHome = new (string Key, string Source, string Trans)[]
                {
                    ("hero-title", "Welcome to our luxury hotel", "Bienvenue dans notre hôtel d'exception"),
                    ("hero-cta", "Book your stay today", "Réservez votre séjour dès aujourd'hui"),
                    ("promo-body", "<p>Enjoy <strong>complimentary</strong> breakfast during your visit.</p>", "<p>Profitez du petit-déjeuner <strong>offert</strong> pendant votre séjour.</p>")
                };

                foreach (var t in frHome)
                {
                    await _repository.UpsertTranslationAsync(new Translation(
                        TranslationId.New(), tenantId, item.Id, new BlockKey(t.Key), new LocaleCode("fr-FR"),
                        ContentHash.FromRawText(t.Source), t.Trans, TranslationProvenance.HumanApproved, now), cancellationToken);
                }
                anyAdded = true;
            }
        }

        if (anyAdded)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
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

    [HttpGet("{id:guid}/preview")]
    [ProducesResponseType(typeof(ContentPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(
        Guid id,
        [FromQuery] string? locale,
        CancellationToken cancellationToken)
    {
        Result<ContentPreviewResponse> result = await _syncService.GetPreviewAsync(id, locale, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            (error, code) => code == ErrorCodes.NotFound
                ? NotFound(ApiError.NotFound("ContentItem"))
                : BadRequest(ApiError.BadRequest(error, code)));
    }

    [HttpPost("simulate-cms-update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult SimulateCmsUpdate(
        [FromServices] OptiFlow.Infrastructure.CMS.LocalTestContentSource localSource,
        [FromQuery] string scenario = "v2")
    {
        if (string.Equals(scenario, "rollback", StringComparison.OrdinalIgnoreCase))
        {
            localSource.SimulateCmsRollback("hotel-home-001", "en");
            return Ok(new
            {
                scenario = "rollback",
                message = "Simulated CMS rollback to original v1 content as v3 snapshot (v3-etag-rollback). Click 'Sync Snapshot' to sync."
            });
        }

        localSource.SimulateCmsUpdateV2("hotel-home-001", "en");
        return Ok(new
        {
            scenario = "v2",
            message = "Simulated CMS update: hero-title modified, amenities-banner added (v2-etag-002). Click 'Sync Snapshot' to sync."
        });
    }
}
