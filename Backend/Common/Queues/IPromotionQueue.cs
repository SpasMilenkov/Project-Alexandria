namespace Common.Queues;

public interface IPromotionQueue
{
    ValueTask QueuePromotionAsync(Guid contentObjectId, string tempObjectKey, CancellationToken ct = default);
}