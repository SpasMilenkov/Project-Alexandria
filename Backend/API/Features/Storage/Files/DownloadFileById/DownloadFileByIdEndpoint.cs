using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.DownloadFileById;

public class DownloadFileByIdEndpoint(
    IStorageService storageService,
    IFileService fileService
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

        Policies(Common.Auth.Policies.RequireUser);

    }

    public override async Task HandleAsync(DownloadFileByIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

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
