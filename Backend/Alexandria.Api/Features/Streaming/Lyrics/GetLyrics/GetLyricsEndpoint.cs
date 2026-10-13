using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Lyrics;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.Lyrics.GetLyrics;

internal sealed class GetLyricsRequest
{
    public required Guid LyricsId { get; set; }
}

internal sealed class GetLyricsEndpoint(ITrackLyricsService lyricsService) : Endpoint<GetLyricsRequest, LyricsDto>
{
    public override void Configure()
    {
        Get("/streaming/lyrics/{lyricsId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetLyricsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            await Send.OkAsync(await lyricsService.GetLyricsForMediaAsync(req.LyricsId, userId, ct), ct);
        }
        catch (CapabilityUnavailableException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(409, ct);
        }
    }
}
