namespace Alexandria.Common.Services;

public interface IPublisherService
{
    Task PublishAsync(byte[] body, string routingKey);
}