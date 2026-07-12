using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.Lyrics.UploadLyrics;

internal sealed class UploadLyricsRequest
{
    public required Guid JobId { get; set; }
    public string? PlainLyrics { get; set; }
    public string? SyncedLyrics { get; set; }
    public bool IsInstrumental { get; set; }
}

internal sealed class UploadLyricsEndpoint(ITrackLyricsService lyricsService) : Endpoint<UploadLyricsRequest>
{
    public override void Configure()
    {
        Post("/streaming/lyrics");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UploadLyricsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await lyricsService.UploadLyricsAsync(req.JobId, userId, req.PlainLyrics, req.SyncedLyrics, ct);

        await Send.NoContentAsync(ct);
    }
}