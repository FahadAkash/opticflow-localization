using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Infrastructure.AI;
using OptiFlow.Infrastructure.Cache;
using OptiFlow.Infrastructure.CMS;
using OptiFlow.Infrastructure.Observability;
using OptiFlow.Infrastructure.Persistence;
using OptiFlow.Infrastructure.Security;

namespace OptiFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddDataProtection();

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICredentialProtector, DataProtectionCredentialProtector>();
        services.AddScoped<IWebhookSignatureVerifier, HmacWebhookSignatureVerifier>();

        string? pgConnection = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(pgConnection))
        {
            services.AddDbContext<OptiFlowDbContext>(options =>
                options.UseNpgsql(pgConnection));
        }
        else
        {
            // Fallback configuration for design-time / override in IntegrationTests
            services.AddDbContext<OptiFlowDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=optiflow;Username=optiflow"));
        }

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OptiFlowDbContext>());
        services.AddScoped<IOptiFlowRepository, OptiFlowRepository>();
        services.AddScoped<IJobQueue, PostgresJobQueue>();

        string? redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "optiflow:";
            });
        }
        services.AddScoped<ICacheService, RedisCacheService>();

        services.AddSingleton<LocalTestContentSource>();
        services.AddHttpClient<OptimizelySaaSContentSource>(client =>
        {
            string? baseUrl = configuration["Optimizely:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri))
            {
                client.BaseAddress = uri;
            }
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IContentSourceResolver, ContentSourceResolver>();

        services.Configure<LmStudioOptions>(configuration.GetSection(LmStudioOptions.SectionName));
        services.AddHttpClient<ILanguageModelClient, LmStudioLanguageModelClient>((sp, client) =>
        {
            LmStudioOptions opts = sp.GetRequiredService<IOptions<LmStudioOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, opts.TimeoutSeconds));
        });

        services.TryAddScoped<IJobNotificationPublisher, NoOpJobNotificationPublisher>();

        return services;
    }
}
