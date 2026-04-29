using Alexandria.Common.Queues;

namespace Alexandria.Tests.Common.Fakes;

public class FakePromotionQueue : IPromotionQueue
{
    public List<(Guid ContentObjectId, string TempKey)> QueuedPromotions { get; } = [];

    public ValueTask QueuePromotionAsync(Guid contentObjectId, string tempObjectKey, CancellationToken ct = default)
    {
        QueuedPromotions.Add((contentObjectId, tempObjectKey));
        return ValueTask.CompletedTask;
    }

    public void Reset() => QueuedPromotions.Clear();
}