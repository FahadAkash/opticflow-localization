using System.Net;
using System.Security.Claims;
using System.Text.Json;
using OptiFlow.Application.Contracts;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception processing {Method} {Path} TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(ApiError.Internal()),
                context.RequestAborted);
        }
    }
}

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            string? tenantClaim = context.User.FindFirstValue("tenant_id")
                                  ?? context.User.FindFirstValue("tid");
            if (Guid.TryParse(tenantClaim, out Guid parsedTenantId) && parsedTenantId != Guid.Empty)
            {
                string actorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                 ?? context.User.FindFirstValue("sub")
                                 ?? "authenticated-user";
                tenantContext.SetTenant(new TenantId(parsedTenantId), actorId);
            }
        }

        await _next(context);
    }
}
