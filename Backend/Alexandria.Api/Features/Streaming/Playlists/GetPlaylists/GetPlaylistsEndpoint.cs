using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming.Playlist;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.GetPlaylists;

internal sealed class GetPlaylistsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
 
internal sealed class GetPlaylistsRequestValidator : Validator<GetPlaylistsRequest>
{
    public GetPlaylistsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
 
internal sealed class GetPlaylistsEndpoint(IPlaylistService playlistService)
    : Endpoint<GetPlaylistsRequest, PaginatedResult<PlaylistDto>>
{
    public override void Configure()
    {
        Get("/playlists");
        Policies(Common.Auth.Policies.RequireUser);
    }
 
    public override async Task HandleAsync(GetPlaylistsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await playlistService.GetByUserAsync(userId, req.Page, req.PageSize, ct);
        await Send.OkAsync(result, ct);
    }
}

