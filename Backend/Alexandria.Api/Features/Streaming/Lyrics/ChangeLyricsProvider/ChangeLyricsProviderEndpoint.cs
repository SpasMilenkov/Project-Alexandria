using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.Lyrics.ChangeLyricsProvider;

internal sealed class ChangeProviderRequest
{
    public required Guid LyricsId { get; set; }
    public required LyricsProvider Provider { get; set; }
}

internal sealed class ChangeProviderEndpoint(ITrackLyricsService lyricsService) : Endpoint<ChangeProviderRequest>
{
    public override void Configure()
    {
        Patch("/streaming/lyrics/change-provider");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(ChangeProviderRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await lyricsService.ChangeProviderAsync(req.LyricsId, userId, req.Provider, ct);

        await Send.NoContentAsync(ct);
    }
}