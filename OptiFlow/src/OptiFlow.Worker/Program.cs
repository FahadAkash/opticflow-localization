var builder = Host.CreateApplicationBuilder(args);

// TODO (M1): Register Application and Infrastructure services
// builder.Services.AddApplicationServices();
// builder.Services.AddInfrastructureServices(builder.Configuration);

// TODO (M1): Register background job processor
// builder.Services.AddHostedService<JobProcessorWorker>();

var host = builder.Build();
host.Run();
