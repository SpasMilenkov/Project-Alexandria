using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Exceptions.Streaming;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Streaming.GetFilesForStreaming;

internal sealed class GetFilesForStreamingRequest
{
    public string? Query { get; set; }
    public Guid? PlaylistId { get; set; }
    public bool IsVideo { get; set; } = false;
    public int Page { get; set; }
    public int PageSize { get; set; }
    public Guid? AnchorFileId { get; set; }
    public Guid? AnchorPlaylistItemId { get; set; }
}

internal sealed class GetFilesForStreamingRequestValidator : Validator<GetFilesForStreamingRequest>
{
    public GetFilesForStreamingRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 500);

        RuleFor(x => x.Query)
            .Empty()
            .When(x => x.AnchorFileId.HasValue || x.AnchorPlaylistItemId.HasValue)
            .WithMessage("Anchor lookup cannot be combined with a search query.");

        RuleFor(x => x.AnchorPlaylistItemId)
            .Null()
            .When(x => !x.PlaylistId.HasValue)
            .WithMessage("AnchorPlaylistItemId requires a playlist source.");

        RuleFor(x => x.AnchorFileId)
            .NotNull()
            .When(x => x.AnchorPlaylistItemId.HasValue)
            .WithMessage("AnchorPlaylistItemId requires AnchorFileId.");
    }
}

internal sealed class GetFilesForStreamingEndpoint(IFileService fileService)
    : Endpoint<GetFilesForStreamingRequest, PaginatedResult<MediaFileDto>>
{
    public override void Configure()
    {
        Get("streaming/files");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetFilesForStreamingRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            await Send.OkAsync(
                await fileService.GetFilesForStreamingAsync(userId, req.Page, req.PageSize, req.Query,
                    req.PlaylistId, req.IsVideo, req.AnchorFileId, req.AnchorPlaylistItemId, ct),
                cancellation: ct);
        }
        catch (PlaylistNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (StreamingAnchorNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
