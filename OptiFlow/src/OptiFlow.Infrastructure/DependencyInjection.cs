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

        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        string? provider = configuration["DatabaseProvider"];

        bool useSqlite = string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase) ||
                         (!string.IsNullOrWhiteSpace(connectionString) &&
                          (connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) ||
                           connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)));

        if (useSqlite)
        {
            string sqliteConn = string.IsNullOrWhiteSpace(connectionString)
                ? "Data Source=optiflow_dev.db"
                : connectionString;

            string rawPath = sqliteConn.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (!Path.IsPathRooted(rawPath))
            {
                string sharedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiFlow");
                Directory.CreateDirectory(sharedRoot);
                sqliteConn = $"Data Source={Path.Combine(sharedRoot, rawPath)}";
            }

            services.AddDbContext<OptiFlowDbContext>(options =>
                options.UseSqlite(sqliteConn));
        }
        else if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<OptiFlowDbContext>(options =>
                options.UseNpgsql(connectionString));
        }
        else
        {
            // Default in-development when no Postgres connection string is configured: use shared local SQLite
            string sharedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptiFlow");
            Directory.CreateDirectory(sharedRoot);
            services.AddDbContext<OptiFlowDbContext>(options =>
                options.UseSqlite($"Data Source={Path.Combine(sharedRoot, "optiflow_dev.db")}"));
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
