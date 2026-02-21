using Common.Services;
using FastEndpoints;

namespace API.Features.Users.GetUserFileCount;

sealed class GetUserFileCountPerUserRequest
{
    public Guid UserId { get; set; }
    public bool DeletedOnly { get; set; }
}

sealed class GetUserFileCountPerUserEndpoint(IFileService fileService) : Endpoint<GetUserFileCountPerUserRequest, int>
{
    public override void Configure()
    {
        Get("users/file-count");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(GetUserFileCountPerUserRequest req, CancellationToken ct)
    {
        await Send.OkAsync(await fileService.GetFileCountPerUser(req.UserId, req.DeletedOnly, ct), ct);
    }
}
