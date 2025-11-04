using Common.Config;
using FastEndpoints;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace API.Features.Storage.GenerateSignedUrlEndpoint;

public class GenerateSignedUrlEndpoint(
    IMinioClient minio,
    IOptions<S3Config> options
) : Endpoint<GenerateSignedUrlRequest, GenerateSignedUrlResponse>
{
    public override void Configure()
    {
        Post("/files/signed-url");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GenerateSignedUrlRequest req, CancellationToken ct)
    {
        var bucket = options.Value.UploadBucket;

        var objectName = string.IsNullOrWhiteSpace(req.Path)
            ? req.Name
            : $"{req.Path.TrimEnd('/')}/{req.Name}";

        var presignedUrl = await minio.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithExpiry((int)req.Expiry.TotalSeconds) // seconds
        );

        await Send.OkAsync(new GenerateSignedUrlResponse
        {
            Url = presignedUrl,
            ExpiresAt = DateTime.UtcNow.Add(req.Expiry)
        }, ct);
    }
}