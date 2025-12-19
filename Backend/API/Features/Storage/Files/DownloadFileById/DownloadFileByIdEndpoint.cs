using Common.Config;
using Common.Services;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace API.Features.Storage.Files.DownloadFileById;

public class DownloadFileByIdEndpoint(
    IStorageService storageService,
    IOptions<S3Config> options
) : Endpoint<DownloadFileByIdRequest>
{
    public override void Configure()
    {
        AllowAnonymous();
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
        var fileMetadata = await storageService.GetFileMetadata(req.Id, ct);
    
        if (fileMetadata == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var bucket = options.Value.UploadBucket;
        var objectName = fileMetadata.Path.Replace($"{bucket}/", "");

        HttpContext.Response.ContentType = fileMetadata.MimeType;
        HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{System.Net.WebUtility.UrlEncode(fileMetadata.Name)}\"";
        HttpContext.Response.ContentLength = (long)fileMetadata.Size;

        // This now uses TRUE streaming - ~80KB memory usage instead of 2GB
        await using var fileStream = await storageService.DownloadFile(bucket, objectName, ct);
        await fileStream.CopyToAsync(HttpContext.Response.Body, ct);
    }
}