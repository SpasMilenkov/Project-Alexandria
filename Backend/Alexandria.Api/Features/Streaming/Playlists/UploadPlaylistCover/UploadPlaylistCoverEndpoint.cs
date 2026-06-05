using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.Playlists.UploadPlaylistCover;

internal sealed class UploadPlaylistCoverRequest
{
    public Guid PlaylistId { get; set; }
    public required string MimeType { get; set; }
    public int FileSize { get; set; }
}

internal sealed class UploadPlaylistCoverRequestValidator : Validator<UploadPlaylistCoverRequest>
{
    public UploadPlaylistCoverRequestValidator()
    {
        RuleFor(x => x.PlaylistId).NotNull().WithMessage("PlaylistId is required");
        RuleFor(x => x.MimeType).NotNull().WithMessage("MimeType is required");
        RuleFor(x => x.FileSize)
            .LessThanOrEqualTo(4 * 1024 * 1024)
            .WithMessage("File too large. Maximum file size is 4MB");
    }
}

internal sealed class UploadPlaylistCoverResponse
{
    public required string UploadUrl { get; set; }
}

internal sealed class UploadPlaylistCoverEndpoint(IStorageService storageService)
    : Endpoint<UploadPlaylistCoverRequest, UploadPlaylistCoverResponse>
{
    public override void Configure()
    {
        Post("/streaming/playlists/cover");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UploadPlaylistCoverRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var presignedUrl =
            await storageService.GetPlaylistCoverUploadUrlAsync(req.PlaylistId, userId, req.MimeType, ct);

        await Send.OkAsync(new UploadPlaylistCoverResponse
        {
            UploadUrl = presignedUrl
        }, ct);
    }
}