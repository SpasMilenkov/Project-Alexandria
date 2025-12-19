using Common.Config;
using Common.Services;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace API.Features.Storage.Files.DeleteFile;

public class DeleteFileEndpoint : Endpoint<DeleteFileRequest>
{
    private readonly IStorageService _storage;
    private readonly IOptions<S3Config> _options;

    public DeleteFileEndpoint(IStorageService storage, IOptions<S3Config> options)
    {
        _storage = storage;
        _options = options;
    }

    public override void Configure()
    {
        Delete("/files/{path}");
        Description(x => x.WithTags("Files"));

        Summary(s =>
        {
            s.Summary = "Soft delete a file (marks as deleted, keeps in storage)";
            s.Description =
                "Performs a soft delete - marks the file as deleted in the database but keeps it in MinIO storage";
            s.Responses[200] = "File deleted successfully";
            s.Responses[404] = "File not found";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync(DeleteFileRequest req, CancellationToken ct)
    {
        var bucketName = _options.Value.UploadBucket;
        if (bucketName is null) ThrowError("Invalid bucket configuration");

        try
        {
            await _storage.DeleteFile(bucketName, req.Path, ct, hardDelete: false);
            await Send.OkAsync(new { Message = "File soft deleted successfully", Path = req.Path }, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
        catch (Exception ex)
        {
            ThrowError($"Delete failed: {ex.Message}");
        }
    }
}