using FastEndpoints;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using MinioConfig = API.Config.MinioConfig;

namespace API.Features.DownloadFile;

public class DownloadFileEndpoint(
    IMinioClient minio,
    IOptions<MinioConfig> options
) : Endpoint<DownloadFileRequest>
{
    public override void Configure()
    {
        AllowAnonymous();
        Get("/file");
    }

    public override async Task HandleAsync(DownloadFileRequest req, CancellationToken ct)
    {
        var bucket = options.Value.UploadBucket;

        var objectName = string.IsNullOrWhiteSpace(req.Path)
            ? req.Name
            : $"{req.Path.TrimEnd('/')}/{req.Name}";

        HttpContext.Response.ContentType = "application/octet-stream";
        HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{req.Name}\"";

        // Stream object from MinIO directly into response
        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithCallbackStream(async (stream, token) =>
            {
                await stream.CopyToAsync(HttpContext.Response.Body, token);
            }), ct);
    }
}