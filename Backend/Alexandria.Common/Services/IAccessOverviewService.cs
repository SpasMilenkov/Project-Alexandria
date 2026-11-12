using Alexandria.Dto.Audit;

namespace Alexandria.Common.Services;

public interface IAccessOverviewService
{
    /// <summary>Account and sign-in statistics over the trailing <paramref name="windowDays"/>.</summary>
    Task<AccessOverview> GetOverviewAsync(int windowDays, CancellationToken ct = default);
}
