using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.DownloadVErsionById;

sealed class DownloadVersionByIdRequest
{
    public Guid Id { get; set; }
}

sealed class DownloadVersionByIdEndpoint(
    IContentStorageService storageService,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork)
    : Endpoint<DownloadVersionByIdRequest, DownloadInfo>
{
    public override void Configure()
    {
        Get("/files/versions/{id}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DownloadVersionByIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var download = await storageService.GetFilVersioneDownloadDetails(req.Id, userId, ct);

        auditWriter.Record(new AuditEntry
        {
            EventCode = AuditEventCode.FileVersionDownloaded,
            OperationType = OperationType.Read,
            EntityType = EntityType.FileVersion,
            EntityId = req.Id,
            OwnerId = userId,
            ParentEntityId = download.FileId,
            EntityName = download.Info.FileName,
            Metadata = new Dictionary<string, object?>
            {
                ["file_id"] = download.FileId,
                ["version_number"] = download.VersionNumber,
                ["mime_type"] = download.Info.MimeType,
            },
        });
        await unitOfWork.SaveChangesAsync(ct);

        await Send.OkAsync(download.Info, ct);
    }
}