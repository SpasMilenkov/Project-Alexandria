using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Dto.Extensions;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public partial class StreamHistoryService(
    IUnitOfWork uow,
    ILogger<StreamHistoryService> logger) : IStreamHistoryService
{
    /// <inheritdoc/>
    public async Task<StreamHistoryResponse?> GetByUserAndFileAsync(
        Guid userId,
        Guid fileId,
        CancellationToken ct = default)
    {
        var history = await uow.StreamingHistories.GetByUserAndFileAsync(userId, fileId, ct);
        return history?.ToResponse();
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<StreamHistoryResponse>> FindHistoryAsync(
        StreamHistoryQuery query,
        CancellationToken ct = default)
    {
        var result = await uow.StreamingHistories.FindHistoryAsync(query, ct);

        return new PaginatedResult<StreamHistoryResponse>
        {
            Items = result.Items.Select(h => h.ToResponse()).ToList(),
            TotalCount = result.TotalCount,
            CurrentPage = result.CurrentPage,
            PageSize = result.PageSize,
            TotalPages = result.TotalPages
        };
    }

    /// <inheritdoc/>
    public async Task UpsertPositionAsync(
        Guid userId,
        Guid fileId,
        long positionSeconds,
        bool completed,
        CancellationToken ct = default)
    {
        await uow.StreamingHistories.UpsertPositionAsync(userId, fileId, positionSeconds, completed, ct);

        LogPositionUpserted(logger, userId, fileId, positionSeconds, completed);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<StreamHistoryResponse>> GetRecentByUserAsync(
        Guid userId,
        int count,
        CancellationToken ct = default)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count,
                "Count must be greater than zero.");

        var histories = await uow.StreamingHistories.GetRecentByUserAsync(userId, count, ct);
        return histories.Select(h => h.ToResponse()).ToList();
    }
}