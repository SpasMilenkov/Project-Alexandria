namespace Alexandria.Common.Services;

public interface IPublisherService
{
    Task Publish(byte[] body, string routingKey);
}