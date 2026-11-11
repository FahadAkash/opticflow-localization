using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OptiFlow.Application.Abstractions;
using OptiFlow.Application.Analytics;
using OptiFlow.Application.Content;
using OptiFlow.Application.Diff;
using OptiFlow.Application.Localization;
using OptiFlow.Application.Merge;
using OptiFlow.Application.Review;
using OptiFlow.Application.TranslationMemory;
using OptiFlow.Application.Validation;
using OptiFlow.Application.Validators;
using OptiFlow.Domain.Interfaces;

namespace OptiFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateConnectionRequestValidator>();

        services.AddScoped<ICmsConnectionService, CmsConnectionService>();
        services.AddScoped<IContentSyncService, ContentSyncService>();
        services.AddScoped<IWebhookIngestionService, WebhookIngestionService>();

        services.AddScoped<IContentDiffEngine, ContentDiffEngine>();
        services.AddScoped<ITranslationMemory, TranslationMemoryService>();
        services.AddScoped<IMergeEngine, MergeEngine>();
        services.AddScoped<ITranslationValidator, DeterministicTranslationValidator>();
        services.AddScoped<ILocalizationOrchestrator, LocalizationOrchestrator>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        return services;
    }
}
