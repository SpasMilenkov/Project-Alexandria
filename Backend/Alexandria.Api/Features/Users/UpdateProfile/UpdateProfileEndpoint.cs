using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Dto.Users;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.UpdateProfile;

sealed class UpdateProfileRequest
{
    public string UserName { get; set; } = string.Empty;
}

sealed class UpdateProfileEndpoint(IUserManagementService userManagementService)
    : Endpoint<UpdateProfileRequest, UserProfileDto>
{
    public override void Configure()
    {
        Patch("/users/profile");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(UpdateProfileRequest req, CancellationToken ct)
    {
        try
        {
            var profile = await userManagementService.UpdateProfileAsync(
                User.GetUserId(),
                new UpdateProfileDto { UserName = req.UserName },
                ct);

            await Send.OkAsync(profile, ct);
        }
        catch (UserNameTakenException)
        {
            AddError(x => x.UserName, "That username is already taken.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
        catch (UserNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
