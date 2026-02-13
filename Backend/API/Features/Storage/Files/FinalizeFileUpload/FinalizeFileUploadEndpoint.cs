using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.FinalizeFileUpload;

sealed class FinalizeFileUploadRequest
{
    public Guid UploadId { get; set; }
    public Guid? DirectoryId { get; set; }
    public required string FileName { get; set; }
}

sealed class FinalizeFileUploadResponse
{
}

sealed class FinalizeFileUploadEndpoint(IStorageService s3Storage)
    : Endpoint<FinalizeFileUploadRequest, FinalizeFileUploadResponse>
{
    public override void Configure()
    {
        Post("files/finalize-upload");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FinalizeFileUploadRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        await s3Storage.FinalizeFileUpload(req.FileName, uploadId: req.UploadId, uploadedBy: userId,
            directoryId: req.DirectoryId, ct);
    }
}