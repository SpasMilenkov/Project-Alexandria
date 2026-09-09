using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files.Streaming;
using FastEndpoints;

namespace Alexandria.Api.Features.Streaming.GetStreamingFile;

internal sealed class GetStreamingFileRequest
{
    public Guid FileId { get; init; }
}

internal sealed class GetStreamingFileEndpoint(IFileService fileService)
    : Endpoint<GetStreamingFileRequest, MediaFileDto>
{
    public override void Configure()
    {
        Get("/streaming/files/{fileId}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetStreamingFileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var file = await fileService.GetStreamingFileAsync(userId, req.FileId, ct);
        if (file is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(file, ct);
    }
}