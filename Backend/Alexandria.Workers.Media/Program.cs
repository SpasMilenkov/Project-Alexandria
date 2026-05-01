using Alexandria.Infrastructure;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Workers.Media;
using Alexandria.Workers.Media.Extensions;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

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