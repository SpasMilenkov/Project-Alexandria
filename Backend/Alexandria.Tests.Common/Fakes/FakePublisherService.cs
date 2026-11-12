using Alexandria.Common.Services;

namespace Alexandria.Tests.Common.Fakes;

/// <summary>Accepts every message and sends nothing, for tests that run services without a broker.</summary>
public sealed class FakePublisherService : IPublisherService
{
    public Task PublishAsync(byte[] body, string routingKey) => Task.CompletedTask;
}
