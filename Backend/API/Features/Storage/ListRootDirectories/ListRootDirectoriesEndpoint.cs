using Common.Config;
using Common.Services;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace API.Features.Storage.ListRootDirectories;

public class ListRootDirectoriesEndpoint(
    IStorageService storageService,
    IOptions<S3Config> options)
    : EndpointWithoutRequest<ListRootDirectoriesResponse>
{
    public override void Configure()
    {
        Get("/files/list-root-directories");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "List root directories in storage";
            s.Description = "Returns a list of root-level directories in the storage bucket.";
            s.Responses[200] = "Root directories retrieved successfully";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var bucketName = options.Value.UploadBucket;
            if (bucketName is null) ThrowError("Invalid bucket configuration");

            // Ensure bucket exists
            await storageService.EnsureBucketExistsAsync(bucketName, ct);

            // Get all files from database
            var allFiles = await storageService.GetAllFiles(ct);

            // Extract unique root directories from file paths
            var directories = allFiles
                .Select(f => f.Path.Replace($"{bucketName}/", "")) // Remove bucket prefix
                .Where(path => path.Contains('/'))
                .Select(path => path.Split('/')[0])
                .Distinct()
                .OrderBy(dir => dir)
                .ToList();

            await Send.OkAsync(new ListRootDirectoriesResponse
            {
                Directories = directories
            }, ct);
        }
        catch (Exception ex)
        {
            ThrowError($"Failed to list directories: {ex.Message}");
        }
    }
}