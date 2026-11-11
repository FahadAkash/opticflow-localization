using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Api.Hubs;

[Authorize]
public sealed class LocalizationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        string? tenantClaim = Context.User?.FindFirstValue("tenant_id")
                              ?? Context.User?.FindFirstValue("tid");

        if (Guid.TryParse(tenantClaim, out Guid tenantId) && tenantId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildGroupName(new TenantId(tenantId)));
        }

        await base.OnConnectedAsync();
    }

    public static string BuildGroupName(TenantId tenantId) => $"tenant:{tenantId.Value}";
}

public sealed class SignalRJobNotificationPublisher : IJobNotificationPublisher
{
    private readonly IHubContext<LocalizationHub> _hubContext;

    public SignalRJobNotificationPublisher(IHubContext<LocalizationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishJobStatusChangedAsync(
        TenantId tenantId,
        JobId jobId,
        JobStatus status,
        CancellationToken cancellationToken)
    {
        await _hubContext.Clients
            .Group(LocalizationHub.BuildGroupName(tenantId))
            .SendAsync(
                "JobStatusChanged",
                new { jobId = jobId.Value, status = status.ToString() },
                cancellationToken);
    }
}
