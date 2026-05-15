using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.GetFilesForStreaming;

internal sealed class GetFilesForStreamingRequest
{
    public int Page { get; set; }
    public int PageSize { get; set; }
}

internal sealed class GetFilesForStreamingEndpoint(IFileService fileService)
    : Endpoint<GetFilesForStreamingRequest, PaginatedResult<FileResult>>
{
    public override void Configure()
    {
        Get("streaming/files");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetFilesForStreamingRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await Send.OkAsync(await fileService.GetFilesForStreamingAsync(userId, req.Page, req.PageSize, ct),
            cancellation: ct);
    }
}