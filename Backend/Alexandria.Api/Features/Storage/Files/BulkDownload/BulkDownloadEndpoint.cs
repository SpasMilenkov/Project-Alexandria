using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Features.Storage.Files.BulkDownloadInit;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using FastEndpoints;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;

namespace Alexandria.Api.Features.Storage.Files.BulkDownload;

sealed class BulkDownloadRequest
{
    public required string Token { get; set; }
}

sealed class BulkDownloadEndpoint(
    IContentStorageService storageService,
    IMemoryCache cache,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork)
    : Endpoint<BulkDownloadRequest>
{
    public override void Configure()
    {
        Get("files/download-bulk/{token}");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(BulkDownloadRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var archiveName = "archive.zip";

        HttpContext.Response.ContentType = "application/zip";
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{archiveName}\"";

        if (!cache.TryGetValue($"bulk-dl:{req.Token}", out BulkDownloadInitRequest? cachedContent))
            await Send.ErrorsAsync(cancellation: ct);

        if (cachedContent is null)
        {
            await Send.NotFoundAsync(cancellation: ct);
            return;
        }

        cache.Remove($"bulk-dl:{req.Token}");

        // Recorded when the archive starts, since a cancelled stream still handed out content.
        auditWriter.Record(new AuditEntry
        {
            EventCode = AuditEventCode.FilesDownloadedBulk,
            OperationType = OperationType.Read,
            EntityType = EntityType.File,
            OwnerId = userId,
            Metadata = new Dictionary<string, object?>
            {
                ["file_count"] = cachedContent.FileIds.Length,
                ["directory_count"] = cachedContent.DirectoryIds.Length,
                ["file_ids"] = cachedContent.FileIds,
                ["directory_ids"] = cachedContent.DirectoryIds,
            },
        });
        await unitOfWork.SaveChangesAsync(ct);

        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await storageService.StreamBulkZipAsync(
            cachedContent.DirectoryIds,
            cachedContent.FileIds,
            userId,
            HttpContext.Response.BodyWriter.AsStream(),
            ct);
    }
}