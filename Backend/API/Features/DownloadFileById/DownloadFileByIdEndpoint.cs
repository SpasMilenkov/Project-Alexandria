using FastEndpoints;
using Infrastructure.Config;
using Infrastructure.Domain.Services;
using Microsoft.Extensions.Options;

namespace API.Features.DownloadFileById;

public class DownloadFileByIdEndpoint(
    IStorageService storageService,
    IOptions<MinioConfig> options
) : Endpoint<DownloadFileByIdRequest>
{
    public override void Configure()
    {
        AllowAnonymous();
        Get("/file/{id}");

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
        try
        {
            var bucket = options.Value.UploadBucket;
            if (bucket is null) ThrowError("Invalid bucket configuration");

            // Get file metadata from database
            var fileMetadata = await storageService.GetFileMetadata(req.Id, ct);

            if (fileMetadata == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            // Extract object name from file path
            var objectName = fileMetadata.Path.Replace($"{bucket}/", "");

            // Set response headers with database metadata
            HttpContext.Response.ContentType = fileMetadata.MimeType;
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileMetadata.Name}\"";
            HttpContext.Response.Headers.ContentLength = (long)fileMetadata.Size;

            // Stream file from storage
            using var fileStream = await storageService.DownloadFile(bucket, objectName, ct);
            await fileStream.CopyToAsync(HttpContext.Response.Body, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            await Send.NotFoundAsync(ct);
        }
        catch (Exception ex)
        {
            ThrowError($"Download failed: {ex.Message}");
        }
    }
}