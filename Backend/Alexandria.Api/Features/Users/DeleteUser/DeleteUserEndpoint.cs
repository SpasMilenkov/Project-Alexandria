using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.DeleteUser;

public sealed class DeleteUserRequest
{
    public required Guid[] UserIds { get; set; }
}

sealed class DeleteUserEndpoint(IUserManagementService userManagementService) : Endpoint<DeleteUserRequest>
{
    public override void Configure()
    {
        Delete("/users");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(DeleteUserRequest req, CancellationToken ct)
    {
        await userManagementService.DeleteUsersAsync(req.UserIds, ct);
        await Send.OkAsync(new { Message = "Users deleted successfully" }, ct);
    }
}