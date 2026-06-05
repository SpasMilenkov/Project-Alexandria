using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.UpdatePlaylist;

internal sealed class UpdatePlaylistRequest
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }

    public bool? HasCover { get; init; }

    public string? AmbientTheme { get; init; }
}

internal sealed class UpdatePlaylistRequestValidator : Validator<UpdatePlaylistRequest>
{
    public UpdatePlaylistRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(256)
            .When(x => x.Name is not null);

        RuleFor(x => x.Description)
            .MaximumLength(1024)
            .When(x => x.Description is not null);
    }
}

internal sealed class UpdatePlaylistEndpoint(IPlaylistService playlistService)
    : Endpoint<UpdatePlaylistRequest, PlaylistDto>
{
    public override void Configure()
    {
        Patch("/playlists/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UpdatePlaylistRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await playlistService.UpdateAsync(
            req.Id,
            req.Name,
            req.Description,
            req.HasCover,
            req.AmbientTheme,
            userId,
            ct);

        await Send.OkAsync(result, ct);
    }
}