using Infrastructure;
using Infrastructure.DocumentWorker;
using MediaWorkerService;
using MediaWorkerService.Extensions;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"RabbitMQ Host: {config["RabbitMQ:HostName"]}");
Console.WriteLine($"RabbitMQ Port: {config["RabbitMQ:Port"]}");
builder.Services
    .AddDatabase(builder.Configuration)
    .AddS3Storage(builder.Configuration)
    .AddRabbitMqConsumer(builder.Configuration)
    .AddPreviewWorkerServices()
    .AddSWorkerServices();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();