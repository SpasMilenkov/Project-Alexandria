using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.RestrictUser;

sealed class RestrictUserRequest
{
    public Guid UserId { get; set; }
    public DateTime LockoutEndDate { get; set; }
}

sealed class RestrictUserEndpoint(IUserManagementService userManagementService) : Endpoint<RestrictUserRequest>
{
    public override void Configure()
    {
        Patch("/users/restrict");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(RestrictUserRequest req, CancellationToken ct)
    {
        await userManagementService.RestrictUserAsync(req.UserId, req.LockoutEndDate, ct);

        await Send.OkAsync(new { Message = "User restricted successfully" }, ct);
    }
}