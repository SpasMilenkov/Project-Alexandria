using Testcontainers.RabbitMq;

namespace Test.Common.TestContainers;

public sealed class AlexandriaRabbitMqContainer
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:3-management-alpine")
        .WithUsername("test_admin")
        .WithPassword("test_password")
        .WithCleanUp(true)
        .Build();

    public string Host => _container.Hostname;
    public int Port => _container.GetMappedPublicPort(5672);
    public string Username => "test_admin";
    public string Password => "test_password";
    public string VirtualHost => "/";

    public Task StartAsync() => _container.StartAsync();
    public ValueTask DisposeAsync() => _container.DisposeAsync();
}