using Alexandria.Common.Config;
using Alexandria.Common.Helpers;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Dto.DocumentTextStats;
using Microsoft.Extensions.Options;

namespace Alexandria.Services.Monitoring;

public class DocumentTextStatsService(
    IDocumentTextRepository documentText,
    IOptions<DocumentTextSweepConfig> options) : IDocumentTextStatsService
{
    internal const int RecentFailureLimit = 20;

    public async Task<DocumentTextOverviewResponse> GetOverviewAsync(Guid viewerId, CancellationToken ct)
    {
        // The API reads the same settings section as the worker, so both agree on the limit.
        var maxAttempts = options.Value.MaxAttempts;

        var statusCounts = await documentText.GetStatusCountsAsync(ct);
        var waiting = await documentText.CountWaitingAsync(
            DocumentTextFileTypes.Supported.Types, DocumentTextExtractor.CurrentVersion, ct);
        var failures = await documentText.GetRecentFailuresAsync(
            viewerId, maxAttempts, RecentFailureLimit, ct);

        return new DocumentTextOverviewResponse(statusCounts, waiting, failures, maxAttempts);
    }

    public Task<int> RetryFailedAsync(CancellationToken ct) => documentText.ResetFailedAsync(ct);
}