using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Microsoft.Extensions.DependencyInjection;

namespace Alexandria.Services.Monitoring;

public class JobOutcomeTracker(IServiceScopeFactory scopeFactory) : IJobOutcomeTracker
{
    public async Task<(int Failures, int Total)> GetCountsSinceAsync(
        JobType type, DateTime since, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        return await jobs.GetOutcomeCountsSinceAsync(type, since, ct);
    }
}