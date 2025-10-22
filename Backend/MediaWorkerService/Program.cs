using Infrastructure;
using Infrastructure.DocumentWorker;
using MediaWorkerService;
using MediaWorkerService.Extensions;

var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddDatabase(builder.Configuration)
    .AddMinio(builder.Configuration)
    .AddRabbitMqConsumer(builder.Configuration)
    .AddPreviewWorkerServices()
    .AddSWorkerServices();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();