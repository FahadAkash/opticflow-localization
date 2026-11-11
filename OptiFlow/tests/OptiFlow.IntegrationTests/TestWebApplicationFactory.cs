using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;
using OptiFlow.Infrastructure.Persistence;
using Xunit;

namespace OptiFlow.IntegrationTests;

public sealed class SpyLanguageModelClient : ILanguageModelClient
{
    private readonly ConcurrentQueue<TranslationRequest> _requests = new();
    private Func<TranslationRequest, TranslationResult>? _overrideResponder;

    public IReadOnlyCollection<TranslationRequest> RecordedRequests => _requests.ToArray();
    public int CallCount => _requests.Count;

    public void Reset()
    {
        while (_requests.TryDequeue(out _)) { }
        _overrideResponder = null;
    }

    public void ConfigureResponder(Func<TranslationRequest, TranslationResult> responder) =>
        _overrideResponder = responder;

    public Task<bool> WarmUpAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        _requests.Enqueue(request);

        if (_overrideResponder is not null)
        {
            return Task.FromResult(_overrideResponder(request));
        }

        string translated = $"[es] {request.SourceText}";
        return Task.FromResult(new TranslationResult(
            IsSuccess: true,
            RawJson: $"{{\"translated_text\":\"{translated}\",\"confidence\":0.92}}",
            TranslatedText: translated,
            Confidence: 0.92,
            InputTokens: 25,
            OutputTokens: 20,
            LatencyMs: 35,
            ModelName: "gemma-3-4b-test",
            Provider: "SpyLMStudio",
            Status: AiInvocationStatus.Succeeded,
            IsTransientError: false));
    }
}

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _ephemeralSigningSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public SpyLanguageModelClient SpyLlm { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = _ephemeralSigningSecret
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OptiFlowDbContext>>();
            services.RemoveAll<OptiFlowDbContext>();
            services.AddDbContext<OptiFlowDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<ILanguageModelClient>();
            services.AddSingleton<ILanguageModelClient>(SpyLlm);
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        using IServiceScope scope = Services.CreateScope();
        OptiFlowDbContext db = scope.ServiceProvider.GetRequiredService<OptiFlowDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    public HttpClient CreateAuthenticatedClient(Guid tenantId, string actorId = "test-reviewer")
    {
        HttpClient client = CreateClient();
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_ephemeralSigningSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: "OptiFlow",
            audience: "OptiFlow.Api",
            claims:
            [
                new Claim("sub", actorId),
                new Claim("tenant_id", tenantId.ToString())
            ],
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));

        return client;
    }

    public async Task<(TenantId TenantId, ConnectionId ConnectionId)> SeedTenantAndConnectionAsync(
        string slug,
        string webhookSecret = "test-webhook-secret")
    {
        using IServiceScope scope = Services.CreateScope();
        OptiFlowDbContext db = scope.ServiceProvider.GetRequiredService<OptiFlowDbContext>();
        ICredentialProtector protector = scope.ServiceProvider.GetRequiredService<ICredentialProtector>();
        TimeProvider time = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var tenantId = TenantId.New();
        var connId = ConnectionId.New();

        await db.Tenants.AddAsync(new Tenant(tenantId, $"Tenant {slug}", slug, time.GetUtcNow()));
        await db.CmsConnections.AddAsync(new CmsConnection(
            connId,
            tenantId,
            CmsProvider.LocalTest,
            "https://local-cms.test",
            protector.Protect(webhookSecret),
            time.GetUtcNow()));
        await db.SaveChangesAsync();

        return (tenantId, connId);
    }
}
