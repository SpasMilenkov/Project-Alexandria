using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using FastEndpoints;

namespace Alexandria.Api.Features.Users.CreateUser;

sealed class CreateUserRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>Quota in bytes. Null applies the 10 GB default. 0 means unlimited.</summary>
    public long? StorageQuotaBytes { get; set; }
}

sealed class CreateUserEndpoint(IUserManagementService userManagementService) : Endpoint<CreateUserRequest>
{
    public override void Configure()
    {
        Post("/users");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        try
        {
            await userManagementService.CreateUserAsync(
                req.Username, req.Email, req.Password, req.Role, req.StorageQuotaBytes, ct);

            await Send.OkAsync(cancellation: ct);
        }
        catch (UserCreationException ex)
        {
            foreach (var (field, messages) in ex.Errors)
            foreach (var message in messages)
                AddError(field, message);

            await Send.ErrorsAsync(statusCode: 400, cancellation: ct);
        }
    }
}