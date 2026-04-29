using Alexandria.Common.Services;
using Alexandria.Dto.Users;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.UpdateUser;

sealed class UpdateUserRequest
{
    public Guid UserId { get; set; }
    public required UpdateUserDto Payload { get; set; }
}

sealed class UpdateUserEndpoint(IUserManagementService userManagementService)
    : Endpoint<UpdateUserRequest, UserDetailsDto>
{
    public override void Configure()
    {
        Patch("/users/{userId}");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(UpdateUserRequest req, CancellationToken ct)
    {
        var result = await userManagementService.UpdateUser(req.UserId, req.Payload, ct);

        await Send.OkAsync(result, ct);
    }
}