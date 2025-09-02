using FastEndpoints;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace API.Features.ListRootDirectories;

public class ListRootDirectoriesEndpoint(
    IMinioClient minio,
    IOptions<API.Config.MinioConfig> options)
    : EndpointWithoutRequest<ListRootDirectoriesResponse>
{
    public override void Configure()
    {
        Get("/files/list-root-directories");
        AllowAnonymous();
    }

    public override async Task<Task> HandleAsync(CancellationToken ct)
    {
        var bucketName = options.Value.UploadBucket;
        var directories = new List<string>();
        // Prepare ListObjectsArgs
        var listArgs = new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithPrefix("")
            .WithRecursive(false);

        // Subscribe to the IObservable<Item>
        var tcs = new TaskCompletionSource<bool>();

        var items = minio.ListObjectsEnumAsync(listArgs, ct);

        await foreach (var item in items)
        {
            if(item.IsDir) directories.Add(item.Key);
        }
        return Send.OkAsync(new ListRootDirectoriesResponse()
        {
            Directories = directories
        }, ct);
    }
}
