using Alexandria.Common;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using Microsoft.Extensions.Logging;

namespace Alexandria.Services.Streaming;

public sealed partial class StreamHistoryService(
    IUnitOfWork unitOfWork,
    ILogger<StreamHistoryService> logger) : IStreamHistoryService
{
    public async Task<StreamHistoryDto?> GetByFileAsync(Guid fileId, Guid userId, CancellationToken ct = default)
    {
        LogFetchingHistory(fileId, userId);
        var entity = await unitOfWork.StreamingHistories.GetByUserAndFileAsync(userId, fileId, ct);
        return entity is null ? null : StreamHistoryDto.FromEntity(entity);
    }


    public async Task<PaginatedResult<StreamHistoryDto>> FindAsync(Guid userId,
        StreamHistoryQuery query,
        CancellationToken ct = default)
    {
        LogFindingHistory(userId);
        return await unitOfWork.StreamingHistories.FindAsync(userId, query, ct);
    }

    public async Task<PaginatedResult<StreamSessionDto>> GetSessionsAsync(
        Guid streamHistoryId,
        Guid userId,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var history = await unitOfWork.StreamingHistories.GetByIdAndUserIdAsync(streamHistoryId, userId, ct)
                      ?? throw new StreamHistoryNotFoundException(streamHistoryId);

        return await unitOfWork.StreamingHistories.GetSessionsAsync(history.Id, page, pageSize, ct);
    }

    public async Task<StreamSessionDto> StartSessionAsync(
        StartSessionRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        LogStartingSession(request.FileId, userId);

        var history = await unitOfWork.StreamingHistories.GetByUserAndFileAsync(userId, request.FileId, ct);

        if (history is null)
        {
            history = await unitOfWork.StreamingHistories.CreateAsync(new StreamHistory
            {
                UserId = userId,
                FileId = request.FileId,
                PositionSeconds = request.StartPositionSeconds,
                LastAccessedAt = DateTime.UtcNow,
            }, ct);

            LogCreatedHistory(history.Id, request.FileId, userId);
        }
        else
        {
            history.LastAccessedAt = DateTime.UtcNow;
            history.PositionSeconds = request.StartPositionSeconds;
            await unitOfWork.StreamingHistories.UpdateAsync(history, ct);
        }

        var session = await unitOfWork.StreamingHistories.CreateSessionAsync(new StreamSession
        {
            StreamHistoryId = history.Id,
            StartPositionSeconds = request.StartPositionSeconds,
            StartedAt = DateTime.UtcNow,
        }, ct);

        LogSessionStarted(session.Id, history.Id);
        return StreamSessionDto.FromEntity(session);
    }

    public async Task<StreamHistoryDto> CloseSessionAsync(
        Guid sessionId,
        CloseSessionRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        LogClosingSession(sessionId, userId);

        var session = await unitOfWork.StreamingHistories.GetSessionByIdAsync(sessionId, ct)
                      ?? throw new StreamSessionNotFoundException(sessionId);

        if (session.EndedAt.HasValue)
            throw new StreamSessionAlreadyClosedException(sessionId);

        var history = await unitOfWork.StreamingHistories.GetByIdAndUserIdAsync(session.StreamHistoryId, userId, ct)
                      ?? throw new StreamHistoryNotFoundException(session.StreamHistoryId);

        session.EndPositionSeconds = request.EndPositionSeconds;
        session.ListenedSeconds = request.ListenedSeconds;
        session.EndedAt = DateTime.UtcNow;

        var fileDurationSeconds = await unitOfWork.MediaMetadata.GetFileDurationAsync(history.FileId, ct);
        session.ReachedCompletionThreshold = fileDurationSeconds > 0
                                             && request.EndPositionSeconds >= fileDurationSeconds *
                                             StreamingConstants.CompletionThresholdRatio;

        await unitOfWork.StreamingHistories.UpdateSessionAsync(session, ct);

        history.PositionSeconds = request.EndPositionSeconds;
        history.TotalListenedSeconds += request.ListenedSeconds;
        history.LastAccessedAt = DateTime.UtcNow;

        if (request.EndPositionSeconds > history.MaxPositionReachedSeconds)
            history.MaxPositionReachedSeconds = request.EndPositionSeconds;

        if (session.ReachedCompletionThreshold)
        {
            history.TimesCompleted++;
            history.LastCompletedAt = DateTime.UtcNow;
            LogSessionCompleted(sessionId, history.Id);
        }

        var updated = await unitOfWork.StreamingHistories.UpdateAsync(history, ct);
        return StreamHistoryDto.FromEntity(updated);
    }
}