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
            await unitOfWork.StreamingHistories.UpdatePositionAsync(
                history.Id, userId, request.StartPositionSeconds, ct);
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

        var history = await unitOfWork.StreamingHistories.GetByIdAndUserIdAsync(session.StreamHistoryId, userId, ct)
                      ?? throw new StreamHistoryNotFoundException(session.StreamHistoryId);

        if (session.EndedAt.HasValue)
            return StreamHistoryDto.FromEntity(history);

        session.EndPositionSeconds = request.EndPositionSeconds;
        session.ListenedSeconds = request.ListenedSeconds;
        session.EndedAt = DateTime.UtcNow;

        var fileDurationSeconds = await unitOfWork.MediaMetadata.GetFileDurationAsync(history.FileId, ct);
        session.IsQualifiedPlay = ListeningPlayQualification.IsQualified(request.ListenedSeconds, fileDurationSeconds);
        session.PlaybackFinished = HasFinished(request, fileDurationSeconds);

        var updated = await unitOfWork.StreamingHistories.CloseSessionAsync(session, userId, ct);

        return StreamHistoryDto.FromEntity(updated);
    }

    private static bool HasFinished(CloseSessionRequest request, double durationSeconds)
    {
        if (!request.PlaybackFinished || request.ListenedSeconds <= 0) return false;

        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0) return true;

        return request.EndPositionSeconds >= durationSeconds - StreamingConstants.FinishPositionToleranceSeconds;
    }
}
