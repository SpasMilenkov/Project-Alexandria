namespace Builder.Models;

// The admin login is chosen by the user in the wizard rather than generated,
// so they never have to dig through .env to learn their credentials (D8)
public sealed class AdminAccountInput
{
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
