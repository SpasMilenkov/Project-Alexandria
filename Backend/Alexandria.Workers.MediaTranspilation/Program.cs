using Alexandria.Infrastructure;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Workers.MediaTranspilation;
using Alexandria.Workers.MediaTranspilation.Extensions;
using Microsoft.AspNetCore.Builder;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Console.WriteLine("Worker starting");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
    );
    builder.Services.AddHostedService<TranspilationWorker>();
    builder.Services.AddHostedService<TranspilationPollingWorker>();

    builder.Services
        .AddWorkerDatabase(builder.Configuration)
        .AddRabbitMqConsumer(builder.Configuration)
        .AddWorkerServices(builder.Configuration)
        .AddS3Storage(builder.Configuration)
        .AddHealthChecks();

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