using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.Lyrics.DeleteLyrics;

sealed class DeleteLyricsRequest
{
    public required Guid LyricsId { get; set; }
}

sealed class DeleteLyricsEndpoint(ITrackLyricsService lyricsService) : Endpoint<DeleteLyricsRequest>
{
    public override void Configure()
    {
        Delete("/streaming/lyrics/{lyricsId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DeleteLyricsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await lyricsService.RemoveLyricsAsync(req.LyricsId, userId, ct);

        await Send.NoContentAsync(ct);
    }
}