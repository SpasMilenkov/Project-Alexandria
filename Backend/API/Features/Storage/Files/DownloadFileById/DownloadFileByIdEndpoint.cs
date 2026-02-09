using System.Security.Claims;
using Common.Config;
using Common.Services;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace API.Features.Storage.Files.DownloadFileById;

public class DownloadFileByIdEndpoint(
    IStorageService storageService,
    IFileService fileService,
    IOptions<S3Config> options
) : Endpoint<DownloadFileByIdRequest>
{
    public override void Configure()
    {
        Get("/files/{id}");
        Description(x => x.WithTags("Files"));

        Summary(s =>
        {
            s.Summary = "Download a file by ID";
            s.Description = "Downloads a file using its unique database ID.";
            s.Responses[200] = "File downloaded successfully";
            s.Responses[404] = "File not found";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync(DownloadFileByIdRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");

        var userId = Guid.Parse(userIdString);

        var fileMetadata = await fileService.GetUserFileMetadataAsync(req.Id, userId, ct);

        if (fileMetadata == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(await storageService.GetFilePresignedUrl(
            fileMetadata.Id, fileMetadata.ContentHash,
            fileMetadata.FileName,
            TimeSpan.FromSeconds(30)), ct);
    }
}