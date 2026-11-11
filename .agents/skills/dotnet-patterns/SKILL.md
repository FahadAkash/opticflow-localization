---
name: dotnet-patterns
description: >-
  Use this skill when implementing ASP.NET Core services, EF Core data access,
  background workers, or structured API responses in the OptiFlow project.
  Covers Clean Architecture project wiring, EF Core configuration, Worker
  Service patterns, Result<T>, middleware ordering, and OpenTelemetry setup.
---

# .NET 8 Patterns for OptiFlow

## 1. Clean Architecture Wiring

### Program.cs (API)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Application layer registration
builder.Services.AddApplicationServices();

// Infrastructure layer registration
builder.Services.AddInfrastructureServices(builder.Configuration);

// API layer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddNpgsql()
        .AddSource("OptiFlow.*")
        .AddOtlpExporter());

var app = builder.Build();

// Middleware ordering matters:
// 1. Exception handler (outermost)
// 2. HTTPS redirection
// 3. Authentication
// 4. Authorization
// 5. Rate limiting
// 6. Routing / endpoints
app.UseExceptionHandler("/error");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
```

### DI Registration Pattern

```csharp
// In OptiFlow.Application/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<ILocalizationOrchestrator, LocalizationOrchestrator>();
        services.AddScoped<IContentDiffEngine, ContentDiffEngine>();
        // Register all application services here
        return services;
    }
}
```

## 2. EF Core Configuration Pattern

```csharp
// IEntityTypeConfiguration per entity — one file per table
public sealed class ContentItemConfiguration
    : IEntityTypeConfiguration<ContentItem>
{
    public void Configure(EntityTypeBuilder<ContentItem> builder)
    {
        builder.ToTable("content_items");

        builder.HasKey(x => x.Id);

        // Value object conversion
        builder.Property(x => x.TenantId)
            .HasConversion(
                v => v.Value,
                v => new TenantId(v))
            .HasColumnName("tenant_id");

        builder.Property(x => x.ExternalContentKey)
            .HasColumnName("external_content_key")
            .HasMaxLength(500)
            .IsRequired();

        // Composite unique index
        builder.HasIndex(x => new { x.TenantId, x.ConnectionId, x.ExternalContentKey })
            .IsUnique();

        // Relationships
        builder.HasMany(x => x.Versions)
            .WithOne()
            .HasForeignKey(x => x.ContentItemId);
    }
}
```

### Value Object Mapping

```csharp
// Value objects as records
public readonly record struct TenantId(Guid Value);
public readonly record struct ContentKey(string Value);
public readonly record struct ContentHash(string Value);
public readonly record struct JobId(Guid Value);

// In DbContext
protected override void ConfigureConventions(
    ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Properties<TenantId>()
        .HaveConversion<TenantIdConverter>();
}
```

## 3. Worker Service Pattern

```csharp
public sealed class JobProcessorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobProcessorWorker> _logger;
    private readonly TimeProvider _timeProvider;

    public JobProcessorWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<JobProcessorWorker> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job processor starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<IJobProcessor>();

                var claimed = await processor.ClaimAndProcessNextAsync(
                    stoppingToken);

                if (!claimed)
                {
                    // No work — back off
                    await Task.Delay(
                        TimeSpan.FromSeconds(2), _timeProvider, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled error in job processor loop");
                await Task.Delay(
                    TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
            }
        }
    }
}
```

## 4. Result Pattern

```csharp
public sealed record Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }
    public bool IsSuccess { get; }

    private Result(T value)
    {
        Value = value;
        IsSuccess = true;
    }

    private Result(string error, string errorCode)
    {
        Error = error;
        ErrorCode = errorCode;
        IsSuccess = false;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error, string errorCode = "GENERAL")
        => new(error, errorCode);

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, string, TResult> onFailure)
        => IsSuccess
            ? onSuccess(Value!)
            : onFailure(Error!, ErrorCode!);
}
```

## 5. Controller Pattern

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public sealed class LocalizationController : ControllerBase
{
    private readonly ILocalizationOrchestrator _orchestrator;

    public LocalizationController(ILocalizationOrchestrator orchestrator)
        => _orchestrator = orchestrator;

    [HttpPost("jobs")]
    [ProducesResponseType(typeof(JobCreatedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateJob(
        [FromBody] CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orchestrator.CreateJobAsync(
            request, cancellationToken);

        return result.Match<IActionResult>(
            job => AcceptedAtAction(
                nameof(GetJob), new { id = job.Id }, job),
            (error, code) => code switch
            {
                "DUPLICATE" => Conflict(new { error }),
                "NOT_FOUND" => NotFound(new { error }),
                _ => BadRequest(new { error })
            });
    }
}
```

## 6. OpenTelemetry Tracing

```csharp
// Create a named ActivitySource per component
public static class Telemetry
{
    public static readonly ActivitySource Source = new("OptiFlow.Application");
}

// Use it in services
using var activity = Telemetry.Source.StartActivity("ProcessLocalizationJob");
activity?.SetTag("job.id", jobId.Value);
activity?.SetTag("tenant.id", tenantId.Value);
// ... do work ...
activity?.SetTag("job.blocks.total", totalBlocks);
activity?.SetTag("job.blocks.changed", changedBlocks);
```
