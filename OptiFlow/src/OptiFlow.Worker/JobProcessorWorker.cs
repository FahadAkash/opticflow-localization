using OptiFlow.Domain.Entities;
using OptiFlow.Domain.Interfaces;

namespace OptiFlow.Worker;

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
        string workerId = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}"[..32];
        _logger.LogInformation("OptiFlow Worker {WorkerId} starting", workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                IJobQueue jobQueue = scope.ServiceProvider.GetRequiredService<IJobQueue>();
                LocalizationJob? claimed = await jobQueue.ClaimNextAsync(workerId, stoppingToken);

                if (claimed is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), _timeProvider, stoppingToken);
                    continue;
                }

                _logger.LogInformation(
                    "Worker {WorkerId} claimed job {JobId} for tenant {TenantId} (attempt {Attempt})",
                    workerId,
                    claimed.Id.Value,
                    claimed.TenantId.Value,
                    claimed.AttemptCount);

                ILocalizationOrchestrator orchestrator = scope.ServiceProvider.GetRequiredService<ILocalizationOrchestrator>();
                await orchestrator.ProcessAsync(claimed.Id.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in worker poll loop");
                await Task.Delay(TimeSpan.FromSeconds(5), _timeProvider, stoppingToken);
            }
        }
    }
}
