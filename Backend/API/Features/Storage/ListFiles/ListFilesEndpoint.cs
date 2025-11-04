using Common.Config;
using Common.Services;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace API.Features.Storage.ListFiles;

public class ListFilesEndpoint(
    IStorageService storageService,
    IOptions<S3Config> options
) : Endpoint<ListFilesRequest, ListFilesResponse>
{
    public override void Configure()
    {
        Get("/files");
        AllowAnonymous();

        Summary(s =>
        {
            s.Summary = "List files in storage";
            s.Description = "Returns a list of files matching the specified path filter.";
            s.Responses[200] = "Files retrieved successfully";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync(ListFilesRequest req, CancellationToken ct)
    {
        try
        {
            var bucket = options.Value.UploadBucket;
            if (bucket is null) ThrowError("Invalid bucket configuration");

            // Get all files from database
            var allFiles = await storageService.GetAllFiles(ct);

            // Filter files based on the requested path
            var filteredFiles = allFiles.Where(f =>
            {
                var relativePath = f.Path.Replace($"{bucket}/", "");
                return string.IsNullOrEmpty(req.Path) || relativePath.StartsWith(req.Path);
            });

            var files = new List<FileInfoDto>();

            foreach (var file in filteredFiles)
            {
                var relativePath = file.Path.Replace($"{bucket}/", "");

                files.Add(new FileInfoDto
                {
                    Id = file.Id,
                    Name = file.Name,
                    Path = relativePath,
                    Size = (long)file.Size,
                    LastModified = file.UpdatedAt ?? file.CreatedAt,
                    IsDir = false, // Files from database are never directories
                    MimeType = file.MimeType,
                    HasPreview = file.HasPreview,
                    PreviewGeneratedAt = file.PreviewGeneratedAt,
                    UpdatedBy = file.UpdatedBy
                });
            }

            // Sort files by name for consistent ordering
            files = files.OrderBy(f => f.Name).ToList();

            await Send.OkAsync(new ListFilesResponse
            {
                Files = files
            }, ct);
        }
        catch (Exception ex)
        {
            ThrowError($"Failed to list files: {ex.Message}");
        }
    }
}