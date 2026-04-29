using Alexandria.Infrastructure;
using Alexandria.Infrastructure.DocumentWorker;
using Alexandria.Workers.Media;
using Alexandria.Workers.Media.Extensions;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"RabbitMQ Host: {config["RabbitMQ:HostName"]}");
Console.WriteLine($"RabbitMQ Port: {config["RabbitMQ:Port"]}");

builder.Services
    .AddWorkerDatabase(builder.Configuration)
    .AddS3Storage(builder.Configuration)
    .AddRabbitMqConsumer(builder.Configuration)
    .AddPreviewWorkerServices()
    .AddSWorkerServices()
    .AddHealthChecks();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.MapHealthChecks("/health");

await host.RunAsync();