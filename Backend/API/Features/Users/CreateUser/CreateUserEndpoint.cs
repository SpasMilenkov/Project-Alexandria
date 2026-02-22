using Common.Exceptions;
using Common.Services;
using FastEndpoints;
using Models.Enumerators;

sealed class CreateUserRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
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
            var user = await userManagementService.CreateUser(
                req.Username, req.Email, req.Password, req.Role, ct);

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
