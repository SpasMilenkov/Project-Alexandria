using Alexandria.Infrastructure;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Workers.MediaMetadata.Extensions;
using Alexandria.Workers.MediaMetadata.Workers;
using Microsoft.AspNetCore.Builder;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
    );

    builder.Services
        .AddWorkerDatabase(builder.Configuration)
        .AddRabbitMqConsumer(builder.Configuration)
        .AddRabbitMqAsync(builder.Configuration)
        .AddWorkerServices(builder.Configuration)
        .AddS3Storage(builder.Configuration)
        .AddHealthChecks();

    builder.Services.AddHostedService<TriggerConsumerWorker>();
    builder.Services.AddHostedService<AccumulatorWorker>();
    builder.Services.AddHostedService<ResultsConsumerWorker>();
    builder.Services.AddHostedService<TimeoutSweepWorker>();

    var host = builder.Build();

    host.MapHealthChecks("/health");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}