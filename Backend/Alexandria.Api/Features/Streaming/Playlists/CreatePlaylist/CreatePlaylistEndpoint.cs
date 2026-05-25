using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming.Playlist;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.CreatePlaylist;

internal sealed class CreatePlaylistRequest
{
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
 
    /// <summary>
    /// Optional pre-resolved cover URL.
    /// When null and InitialTranspilationJobIds is non-empty, the service falls back
    /// to a random preview from those jobs' files.
    /// TODO: populate this from the cover upload result before calling the endpoint.
    /// </summary>
    public string? CoverUrl { get; init; }
 
    public string? AmbientTheme { get; init; }
    public Guid[] InitialTranspilationJobIds { get; init; } = [];
}
 
internal sealed class CreatePlaylistRequestValidator : Validator<CreatePlaylistRequest>
{
    public CreatePlaylistRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(256);
 
        RuleFor(x => x.Description)
            .MaximumLength(1024)
            .When(x => x.Description is not null);
    }
}
 
internal sealed class CreatePlaylistEndpoint(IPlaylistService playlistService)
    : Endpoint<CreatePlaylistRequest, PlaylistDto>
{
    public override void Configure()
    {
        Post("/playlists");
        Policies(Common.Auth.Policies.RequireUser);
    }
 
    public override async Task HandleAsync(CreatePlaylistRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
 
        var result = await playlistService.CreateAsync(
            req.Name,
            req.Description,
            req.CoverUrl,
            req.AmbientTheme,
            req.InitialTranspilationJobIds,
            userId,
            ct);
 
        await Send.CreatedAtAsync<GetPlaylist.GetPlaylistEndpoint>(
            new { Id = result.Id }, result, cancellation: ct);
    }
}
