using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.AddPlaylistItem;

internal sealed class AddPlaylistItemRequest
{
    public Guid PlaylistId { get; init; }
    public Guid TranspilationJobId { get; init; }
}
 
internal sealed class AddPlaylistItemRequestValidator : Validator<AddPlaylistItemRequest>
{
    public AddPlaylistItemRequestValidator()
    {
        RuleFor(x => x.PlaylistId).NotEmpty();
        RuleFor(x => x.TranspilationJobId).NotEmpty();
    }
}
 
internal sealed class AddPlaylistItemEndpoint(IPlaylistService playlistService)
    : Endpoint<AddPlaylistItemRequest, PlaylistItemDto>
{
    public override void Configure()
    {
        Post("/playlists/{playlistId}/items");
        Policies(Common.Auth.Policies.RequireUser);
    }
 
    public override async Task HandleAsync(AddPlaylistItemRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await playlistService.AddItemAsync(req.PlaylistId, req.TranspilationJobId, userId, ct);
 
        await Send.CreatedAtAsync<GetPlaylist.GetPlaylistEndpoint>(
            new { Id = req.PlaylistId }, result, cancellation: ct);
    }
}

