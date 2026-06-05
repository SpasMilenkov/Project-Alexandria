using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.FinalizeFileUpload;

internal sealed class FinalizeFileUploadRequest
{
    public Guid UploadId { get; set; }
    public Guid? DirectoryId { get; set; }
    public required string FileName { get; set; }
    public bool IsEncrypted { get; set; }
    public byte[]? EncryptionIv { get; set; }
    public byte[]? EncryptionSalt { get; set; }
    public byte[]? IntegrityTag { get; set; }
    public string? EncryptionHint { get; set; }
    public int? IterationCount { get; set; }
}

sealed class FinalizeFileUploadEndpoint(IStorageService s3Storage, IPolicyDispatcher policyDispatcher)
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

        var result = await s3Storage.FinalizeFileUpload(req.FileName,
            uploadId: req.UploadId,
            uploadedBy: userId,
            encryptionIv: req.EncryptionIv,
            encryptionSalt: req.EncryptionSalt,
            integrityTag: req.IntegrityTag,
            encryptionHint: req.EncryptionHint,
            iterationCount: req.IterationCount,
            isEncrypted: req.IsEncrypted, directoryId: req.DirectoryId, ct: ct);
        //TODO: gotta make it job specific, encrypted files are eligible for backup
        if (!req.IsEncrypted)
        {
            await policyDispatcher.DispatchAsync(new FileFinalizedEvent(result.FileId,
                result.VersionId,
                req.DirectoryId,
                result.MimeType,
                req.FileName,
                userId,
                result.IsNewVersion), ct);
        }

        await Send.OkAsync(cancellation: ct);
    }
}