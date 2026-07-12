using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.Lyrics.QueueRefetch;

sealed class QueueRefetchRequest
{
    public required Guid JobId { get; set; }
}

sealed class QueueRefetchEndpoint(ITrackLyricsService lyricsService) : Endpoint<QueueRefetchRequest>
{
    public override void Configure()
    {
        Patch("/streaming/lyrics/refetch/{jobId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(QueueRefetchRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await lyricsService.QueueLyricsRefetchAsync(req.JobId, userId, ct);

        await Send.NoContentAsync(ct);
    }
}