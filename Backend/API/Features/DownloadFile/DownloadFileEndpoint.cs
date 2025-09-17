using FastEndpoints;
using Infrastructure.Domain.Services;
using Microsoft.Extensions.Options;
using MinioConfig = Infrastructure.Config.MinioConfig;

namespace API.Features.DownloadFile;

public class DownloadFileEndpoint(
    IStorageService storageService,
    IOptions<MinioConfig> options
) : Endpoint<DownloadFileRequest>
{
    public override void Configure()
    {
        AllowAnonymous();
        Get("/file");

        Summary(s =>
        {
            s.Summary = "Download a file from storage";
            s.Description = "Downloads a file by name and optional path.";
            s.Responses[200] = "File downloaded successfully";
            s.Responses[404] = "File not found";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync(DownloadFileRequest req, CancellationToken ct)
    {
        try
        {
            var bucket = options.Value.UploadBucket;
            if (bucket is null) ThrowError("Invalid bucket configuration");

            var objectName = string.IsNullOrWhiteSpace(req.Path)
                ? req.Name
                : $"{req.Path.TrimEnd('/')}/{req.Name}";

            // Verify file exists in database and get metadata
            var filePath = $"{bucket}/{objectName}";
            var fileMetadata = await storageService.GetFileByPath(filePath, ct);

            if (fileMetadata == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

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
            await Send.OkAsync(ct, ct);
        }
        catch (Exception ex)
        {
            ThrowError($"Download failed: {ex.Message}");
        }
    }
}