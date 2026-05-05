namespace Alexandria.Common.Services;

public interface IPromotionService
{
    Task<bool> TryPromoteContentObjectAsync(Guid contentObjectId, string tempObjectKey, CancellationToken ct = default);
}