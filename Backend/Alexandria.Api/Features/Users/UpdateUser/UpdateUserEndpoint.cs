using Alexandria.Common.Exceptions;
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
        try
        {
            var result = await userManagementService.UpdateUserAsync(req.UserId, req.Payload, ct);
            await Send.OkAsync(result, ct);
        }
        catch (StorageQuotaBelowUsageException ex)
        {
            AddError(
                x => x.Payload.StorageQuotaBytes,
                $"Storage quota cannot be below current usage of {ex.UsedBytes} bytes.");
            await Send.ErrorsAsync(statusCode: 400, cancellation: ct);
        }
    }
}