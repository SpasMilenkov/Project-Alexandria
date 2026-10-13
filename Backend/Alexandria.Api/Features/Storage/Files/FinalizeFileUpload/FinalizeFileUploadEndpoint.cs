using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using FastEndpoints;
using Microsoft.Extensions.Options;

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

sealed class FinalizeFileUploadEndpoint(
    IStorageService s3Storage,
    IPolicyDispatcher policyDispatcher,
    IPreviewService previewService,
    IOptions<ProcessingCapabilities> capabilities)
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
            if (!capabilities.Value.MediaPreviews &&
                (result.MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                 result.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
                await previewService.GeneratePreviewAsync(result.VersionId, userId, ct: ct);

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
