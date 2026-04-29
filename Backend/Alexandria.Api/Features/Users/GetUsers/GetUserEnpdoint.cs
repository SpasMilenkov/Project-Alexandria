using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Dto.Users;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.GetUsers;

sealed class GetUsersEndpoint(IUserManagementService userManagementService)
    : Endpoint<UserQueryDto, PaginatedResult<UserDetailsDto>>
{
    public override void Configure()
    {
        Get("/users");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(UserQueryDto req, CancellationToken ct)
    {
        await Send.OkAsync(await userManagementService.GetUsers(req, ct), ct);
    }
}