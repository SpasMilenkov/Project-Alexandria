using DocumentWorker.Service;
using DocumentWorker.Service.Extensions;
using Infrastructure;
using Infrastructure.DocumentWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddWorkerDatabase(builder.Configuration)
    .AddS3Storage(builder.Configuration)
    .AddRabbitMqConsumer(builder.Configuration)
    .AddSWorkerServices();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();