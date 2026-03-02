using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Storage.Files.FinalizeFileUpload;

internal sealed class FinalizeFileUploadRequest
{
    public Guid UploadId { get; set; }
    public Guid? DirectoryId { get; set; }
    public required string FileName { get; set; }
}

sealed class FinalizeFileUploadEndpoint(IStorageService s3Storage)
    : Endpoint<FinalizeFileUploadRequest>
{
    public override void Configure()
    {
        Post("files/finalize-upload");
        Policies(Common.Auth.Policies.CanUpload);
    }

    public override async Task HandleAsync(FinalizeFileUploadRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await s3Storage.FinalizeFileUpload(req.FileName, uploadId: req.UploadId, uploadedBy: userId,
            directoryId: req.DirectoryId, ct);

        await Send.OkAsync(cancellation: ct);
    }
}
