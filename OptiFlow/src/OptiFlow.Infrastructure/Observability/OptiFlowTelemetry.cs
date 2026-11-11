using System.Diagnostics;
using System.Diagnostics.Metrics;
using OptiFlow.Domain.Enums;
using OptiFlow.Domain.Interfaces;
using OptiFlow.Domain.ValueObjects;

namespace OptiFlow.Infrastructure.Observability;

public static class OptiFlowTelemetry
{
    public const string ServiceNamePrefix = "OptiFlow";
    public static readonly ActivitySource Source = new("OptiFlow.Observability");
    public static readonly Meter Meter = new("OptiFlow.Metrics", "1.0.0");

    public static readonly Counter<long> AiTokensCounter =
        Meter.CreateCounter<long>("optiflow.ai.tokens", unit: "tokens", description: "AI tokens processed");

    public static readonly Counter<long> TranslationMemoryHitsCounter =
        Meter.CreateCounter<long>("optiflow.tm.hits", unit: "blocks", description: "Approved TM reuse count");

    public static readonly Histogram<double> JobBlockLatencyMs =
        Meter.CreateHistogram<double>("optiflow.ai.latency_ms", unit: "ms", description: "AI inference latency");
}

public sealed class NoOpJobNotificationPublisher : IJobNotificationPublisher
{
    public Task PublishJobStatusChangedAsync(
        TenantId tenantId,
        JobId jobId,
        JobStatus status,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
