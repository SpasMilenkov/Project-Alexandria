using DocumentWorker.Service;
using DocumentWorker.Service.Extensions;
using Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services
    .AddRabbitMqConsumer(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();