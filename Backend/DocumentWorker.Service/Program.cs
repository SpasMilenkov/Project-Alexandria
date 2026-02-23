using DocumentWorker.Service;
using DocumentWorker.Service.Extensions;
using Infrastructure;
using Infrastructure.DocumentWorker;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddWorkerDatabase(builder.Configuration)
    .AddS3Storage(builder.Configuration)
    .AddRabbitMqConsumer(builder.Configuration)
    .AddSWorkerServices()
    .AddHealthChecks();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.MapHealthChecks("/health");

host.Run();
