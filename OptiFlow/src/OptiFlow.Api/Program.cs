using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OptiFlow.Api.Hubs;
using OptiFlow.Api.Middleware;
using OptiFlow.Application;
using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;
using OptiFlow.Infrastructure;
using OptiFlow.Infrastructure.Observability;
using OptiFlow.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddSingleton(sp =>
{
    IConfiguration config = sp.GetRequiredService<IConfiguration>();
    IHostEnvironment env = sp.GetRequiredService<IHostEnvironment>();
    string? configuredKey = config["Jwt:SigningKey"];
    byte[] keyBytes;
    if (!string.IsNullOrWhiteSpace(configuredKey))
    {
        keyBytes = Encoding.UTF8.GetBytes(configuredKey);
    }
    else if (env.IsDevelopment() || env.IsEnvironment("Testing"))
    {
        keyBytes = RandomNumberGenerator.GetBytes(32);
    }
    else
    {
        throw new InvalidOperationException("Jwt:SigningKey must be configured via vault or User Secrets outside Development.");
    }

    return new SymmetricSecurityKey(keyBytes);
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddScoped<IJobNotificationPublisher, SignalRJobNotificationPublisher>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<SymmetricSecurityKey>((options, key) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "OptiFlow",
            ValidateAudience = true,
            ValidAudience = "OptiFlow.Api",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                string? accessToken = context.Request.Query["access_token"];
                PathString path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/localization"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardDev", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("default", limiter =>
    {
        limiter.PermitLimit = 120;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("OptiFlow.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddSource("OptiFlow.*");

        string? otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(OptiFlowTelemetry.Meter.Name);
        string? otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    });

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OptiFlowDbContext>();
        db.Database.EnsureCreated();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DashboardDev");
}
else if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers().RequireRateLimiting("default");
app.MapHub<LocalizationHub>("/hubs/localization");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/v1/dev/token", async (
        DevTokenRequest req,
        SymmetricSecurityKey key,
        IOptiFlowRepository repository,
        IUnitOfWork unitOfWork,
        ICredentialProtector protector,
        TimeProvider timeProvider,
        CancellationToken ct) =>
    {
        var tenantId = new TenantId(req.TenantId == Guid.Empty ? Guid.Parse("11111111-1111-1111-1111-111111111111") : req.TenantId);
        Tenant? existingTenant = await repository.GetTenantAsync(tenantId, ct);
        if (existingTenant is null)
        {
            await repository.AddTenantAsync(new Tenant(tenantId, "Dev Hotel Tenant", "dev-hotel", timeProvider.GetUtcNow()), ct);
            await repository.AddConnectionAsync(new CmsConnection(
                ConnectionId.New(),
                tenantId,
                CmsProvider.LocalTest,
                "https://local-cms.test",
                protector.Protect("dev-webhook-secret"),
                timeProvider.GetUtcNow()), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }

        var claims = new[]
        {
            new Claim("sub", string.IsNullOrWhiteSpace(req.ActorId) ? "dev-reviewer" : req.ActorId),
            new Claim("tenant_id", tenantId.Value.ToString())
        };

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: "OptiFlow",
            audience: "OptiFlow.Api",
            claims: claims,
            expires: timeProvider.GetUtcNow().UtcDateTime.AddHours(8),
            signingCredentials: creds);

        return Results.Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(jwt),
            tenantId = tenantId.Value
        });
    }).AllowAnonymous();
}

app.Run();

public sealed record DevTokenRequest(Guid TenantId, string? ActorId = null);

public partial class Program;
