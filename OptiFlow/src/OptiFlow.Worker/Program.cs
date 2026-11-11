using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OptiFlow.Application;
using OptiFlow.Infrastructure;
using OptiFlow.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<JobProcessorWorker>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("OptiFlow.Worker"))
    .WithTracing(tracing =>
    {
        tracing.AddSource("OptiFlow.*");
        string? otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    });

var host = builder.Build();
host.Run();
