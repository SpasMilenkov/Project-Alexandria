using Alexandria.Common.Services;

namespace Alexandria.Tests.Common.Fakes;

public sealed class FakeMessageBrokerStatus : IMessageBrokerStatus
{
    public bool IsEnabled { get; init; } = true;
    public bool IsAvailable { get; init; } = true;
}
