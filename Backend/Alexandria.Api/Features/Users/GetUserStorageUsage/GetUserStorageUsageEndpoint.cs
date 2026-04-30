using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.GetUserStorageUsage;

sealed class GetUserStorageUsageRequest
{
    public Guid UserId { get; set; }
    public bool DeletedOnly { get; set; }
}

sealed class GetUserStorageUsageEndpoint(IFileService fileService) : Endpoint<GetUserStorageUsageRequest, long>
{
    public override void Configure()
    {
        Get("users/file-size");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(GetUserStorageUsageRequest req, CancellationToken ct)
    {
        await Send.OkAsync(await fileService.GetFileSizePerUserAsync(req.UserId, req.DeletedOnly, ct), ct);
    }
}