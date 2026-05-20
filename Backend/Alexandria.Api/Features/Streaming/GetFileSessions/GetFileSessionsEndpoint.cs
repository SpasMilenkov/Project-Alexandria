using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.GetFileSessions;

internal sealed class GetFileSessionsRequest
{
    public Guid StreamHistoryId { get; init; }
}

internal sealed class GetFileSessionsEndpoint(IStreamHistoryService historyService)
    : Endpoint<GetFileSessionsRequest, IEnumerable<StreamSessionDto>>
{
    public override void Configure()
    {
        Get("/stream-history/{StreamHistoryId}/sessions");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetFileSessionsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var sessions = await historyService.GetSessionsAsync(req.StreamHistoryId, userId, ct);
        await Send.OkAsync(sessions, ct);
    }
}