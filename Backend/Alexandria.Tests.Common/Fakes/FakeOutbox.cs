using Alexandria.Common.Services;

namespace Alexandria.Tests.Common.Fakes;

/// <summary>Accepts every message and stores nothing, for tests that run services without a broker.</summary>
public sealed class FakeOutbox : IOutbox
{
    public Task EnqueueAsync(string routingKey, byte[] body, CancellationToken ct) => Task.CompletedTask;

    public Task EnqueueJobAsync(string routingKey, Guid jobId, CancellationToken ct) => Task.CompletedTask;
}
