using FastEndpoints;
using Microsoft.Extensions.Options;
using Minio;
using Minio.ApiEndpoints;
using Minio.DataModel;
using Minio.DataModel.Args;

namespace API.Features.ListFiles;

public class ListFilesEndpoint(
    IMinioClient minio,
    IOptions<API.Config.MinioConfig> options
) : Endpoint<ListFilesRequest, ListFilesResponse>
{
    public override void Configure()
    {
        Get("/files");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListFilesRequest req, CancellationToken ct)
    {
        var bucket = options.Value.UploadBucket;
        
        IAsyncEnumerable<Item> items = minio.ListObjectsEnumAsync(
            new ListObjectsArgs()
                .WithBucket(bucket)
                .WithPrefix(req.Path ?? string.Empty)
                .WithRecursive(true), ct);

        var files = new List<FileInfoDto>();

        await foreach (var item in items)
        {
            files.Add(new FileInfoDto
            {
                Name = item.Key,
                Size = (long)item.Size,
                LastModified = item.LastModifiedDateTime,
                IsDir = item.IsDir
            });
        }

        await Send.OkAsync(new ListFilesResponse
        {
            Files = files
        }, ct);
    }
}
