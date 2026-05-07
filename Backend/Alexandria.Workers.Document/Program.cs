using Alexandria.Infrastructure;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Workers.Document;
using Alexandria.Workers.Document.Extensions;
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
    );


    builder.Services
        .AddWorkerDatabase(builder.Configuration)
        .AddS3Storage(builder.Configuration)
        .AddRabbitMqConsumer(builder.Configuration)
        .AddWorkerServices()
        .AddHealthChecks();


    builder.Services.AddHostedService<Worker>();
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
