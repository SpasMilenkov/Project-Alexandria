using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.GetFileSessions;

internal sealed class GetFileSessionsRequest
{
    public Guid StreamHistoryId { get; init; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

internal sealed class GetFileSessionsEndpoint(IStreamHistoryService historyService)
    : Endpoint<GetFileSessionsRequest, PaginatedResult<StreamSessionDto>>
{
    public override void Configure()
    {
        Get("/stream-history/{StreamHistoryId}/sessions");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetFileSessionsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var sessions = await historyService.GetSessionsAsync(req.StreamHistoryId, userId, req.Page, req.PageSize, ct);
        await Send.OkAsync(sessions, ct);
    }
}