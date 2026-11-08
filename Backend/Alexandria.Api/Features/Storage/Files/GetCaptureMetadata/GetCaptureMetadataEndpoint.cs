using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.GetCaptureMetadata;

internal sealed class GetCaptureMetadataRequest
{
    public Guid FileId { get; set; }

    /// <summary>
    /// The version to read. Omit for the file's current version.
    /// </summary>
    [QueryParam]
    public Guid? VersionId { get; set; }
}

/// <summary>
/// A version's capture metadata, including its exact location, for the file's owner only.
/// </summary>
internal sealed class GetCaptureMetadataEndpoint(IUnitOfWork unitOfWork)
    : Endpoint<GetCaptureMetadataRequest, CaptureMetadataResponse>
{
    public override void Configure()
    {
        Get("/files/{fileId}/capture-metadata");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetCaptureMetadataRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var file = await unitOfWork.Files.GetByIdAsync(req.FileId, ct);
        if (file is null || file.DeletedAt is not null || file.OwnerId != userId)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var versionId = req.VersionId ?? file.CurrentVersionId;
        if (versionId is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var versionExists = await unitOfWork.FileVersions.ExistsAsync(
            v => v.Id == versionId && v.FileId == file.Id && v.DeletedAt == null, ct);
        if (!versionExists)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var metadata = await unitOfWork.CaptureMetadata.GetByVersionIdAsync(versionId.Value, ct);

        var response = new CaptureMetadataResponse
        {
            FileVersionId = versionId.Value,
            IsExtracted = metadata is not null,
            HasLocation = metadata?.Latitude is not null && metadata.Longitude is not null,
            Width = metadata?.Width,
            Height = metadata?.Height,
            CapturedAt = metadata?.CapturedAt,
            CapturedOffsetMinutes = metadata?.CapturedOffsetMinutes,
            Latitude = metadata?.Latitude,
            Longitude = metadata?.Longitude,
            CameraMake = metadata?.CameraMake,
            CameraModel = metadata?.CameraModel,
            LensModel = metadata?.LensModel,
            Software = metadata?.Software,
            FocalLengthMm = metadata?.FocalLengthMm,
            FocalLength35mm = metadata?.FocalLength35mm,
            FNumber = metadata?.FNumber,
            ExposureSeconds = metadata?.ExposureSeconds,
            Iso = metadata?.Iso,
        };

        await Send.OkAsync(response, ct);
    }
}
