using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.DownloadFileById;

public class DownloadFileByIdEndpoint(
    IStorageService storageService
) : Endpoint<DownloadFileByIdRequest, DownloadInfo>
{
    public override void Configure()
    {
        Get("/files/download/{id}");
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

        await Send.OkAsync(await storageService.GetFileDownloadDetails(
            req.Id, userId, ct), ct);
    }
}