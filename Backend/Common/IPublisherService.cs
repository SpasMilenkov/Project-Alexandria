namespace Common;

public interface IPublisherService
{
    Task Publish(byte[] body, string routingKey);
}