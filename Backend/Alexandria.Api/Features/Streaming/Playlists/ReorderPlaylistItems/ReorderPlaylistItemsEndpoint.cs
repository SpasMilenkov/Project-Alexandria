using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.ReorderPlaylistItems;

internal sealed class ReorderPlaylistItemsRequest
{
    public Guid PlaylistId { get; init; }
 
    /// <summary>
    /// Full ordered sequence of item IDs. Every non-deleted item in the playlist
    /// should appear exactly once. Items absent from this list are left at their
    /// current position.
    /// </summary>
    public Guid[] OrderedItemIds { get; init; } = [];
}
 
internal sealed class ReorderPlaylistItemsRequestValidator : Validator<ReorderPlaylistItemsRequest>
{
    public ReorderPlaylistItemsRequestValidator()
    {
        RuleFor(x => x.PlaylistId).NotEmpty();
        RuleFor(x => x.OrderedItemIds).NotEmpty();
        RuleFor(x => x.OrderedItemIds)
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("OrderedItemIds must not contain duplicates.");
    }
}
 
internal sealed class ReorderPlaylistItemsEndpoint(IPlaylistService playlistService)
    : Endpoint<ReorderPlaylistItemsRequest>
{
    public override void Configure()
    {
        Put("/playlists/{playlistId}/items/order");
        Policies(Common.Auth.Policies.RequireUser);
    }
 
    public override async Task HandleAsync(ReorderPlaylistItemsRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await playlistService.ReorderItemsAsync(req.PlaylistId, req.OrderedItemIds, userId, ct);
        await Send.NoContentAsync(ct);
    }
}

