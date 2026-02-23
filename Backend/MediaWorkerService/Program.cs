using Infrastructure;
using Infrastructure.DocumentWorker;
using MediaWorkerService;
using MediaWorkerService.Extensions;
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

host.Run();
