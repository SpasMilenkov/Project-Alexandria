using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.GetFilesForStreaming;

internal sealed class GetFilesForStreamingRequest
{
    public string? Query { get; set; }
    public Guid? PlaylistId { get; set; }
    public bool IsVideo { get; set; } = false;
    public int Page { get; set; }
    public int PageSize { get; set; }
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
        await Send.OkAsync(
            await fileService.GetFilesForStreamingAsync(userId, req.Page, req.PageSize, req.Query, req.PlaylistId,
                req.IsVideo, ct),
            cancellation: ct);
    }
}