using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Features.Storage.Files.BulkDownloadInit;
using Alexandria.Common.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;

namespace Alexandria.Api.Features.Storage.Files.BulkDownload;

sealed class BulkDownloadRequest
{
    public required string Token { get; set; }
}

sealed class BulkDownloadEndpoint(IStorageService storageService, IMemoryCache cache) : Endpoint<BulkDownloadRequest>
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

        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        await storageService.StreamBulkZipAsync(
            cachedContent.DirectoryIds,
            cachedContent.FileIds,
            userId,
            HttpContext.Response.BodyWriter.AsStream(),
            ct);
    }
}