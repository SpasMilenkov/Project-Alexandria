using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Dto.Users;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.GetProfileStats;

sealed class GetProfileStatsEndpoint(IUserManagementService userManagementService)
    : EndpointWithoutRequest<UserProfileStatsDto>
{
    public override void Configure()
    {
        Get("/users/profile/stats");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var stats = await userManagementService.GetProfileStatsAsync(User.GetUserId(), ct);
            await Send.OkAsync(stats, cancellation: ct);
        }
        catch (UserNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
