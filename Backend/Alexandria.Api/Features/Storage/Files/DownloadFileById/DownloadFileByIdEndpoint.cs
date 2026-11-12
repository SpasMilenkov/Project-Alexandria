using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.Files;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.DownloadFileById;

public class DownloadFileByIdEndpoint(
    IContentStorageService storageService,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork
) : Endpoint<DownloadFileByIdRequest, DownloadInfo>
{
    public override void Configure()
    {
        Get("/files/download/{id}");
        Description(x => x.WithTags("Files"));

        Summary(s =>
        {
            s.Summary = "Download a file by ID";
            s.Description = "Downloads a file using its unique database ID.";
            s.Responses[200] = "File downloaded successfully";
            s.Responses[404] = "File not found";
            s.Responses[500] = "Internal server error";
        });

        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(DownloadFileByIdRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var info = await storageService.GetFileDownloadDetails(req.Id, userId, ct);

        auditWriter.Record(new AuditEntry
        {
            EventCode = AuditEventCode.FileDownloaded,
            OperationType = OperationType.Read,
            EntityType = EntityType.File,
            EntityId = req.Id,
            OwnerId = userId,
            EntityName = info.FileName,
            Metadata = new Dictionary<string, object?> { ["mime_type"] = info.MimeType },
        });
        await unitOfWork.SaveChangesAsync(ct);

        await Send.OkAsync(info, ct);
    }
}