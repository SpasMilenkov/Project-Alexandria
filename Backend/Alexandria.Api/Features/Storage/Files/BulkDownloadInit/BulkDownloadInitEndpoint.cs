using FastEndpoints;
using Microsoft.Extensions.Caching.Memory;

namespace Alexandria.Api.Features.Storage.Files.BulkDownloadInit;

sealed class BulkDownloadInitRequest
{
    public required Guid[] DirectoryIds { get; set; }
    public required Guid[] FileIds { get; set; }
}

sealed class BulkDownloadInitResponse
{
    public required string Token { get; set; }
}

sealed class BulkDownloadInitEndpoint(IMemoryCache cache) : Endpoint<BulkDownloadInitRequest, BulkDownloadInitResponse>
{
    public override void Configure()
    {
        Post("files/bulk-download/init");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(BulkDownloadInitRequest req, CancellationToken ct)
    {
        var token = Guid.NewGuid().ToString();
        cache.Set(
            $"bulk-dl:{token}",
            req,
            TimeSpan.FromSeconds(30)
        );

        await Send.OkAsync(new BulkDownloadInitResponse { Token = token }, ct);
    }
}