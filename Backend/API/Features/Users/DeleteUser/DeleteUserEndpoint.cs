using Common.Services;
using FastEndpoints;

namespace API.Features.Users.DeleteUser;

sealed class DeleteUserRequest
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
        await userManagementService.DeleteUsers(req.UserIds, ct);
        await Send.OkAsync(new { Message = "Users deleted successfully" }, ct);
    }
}
