namespace API.Features.Auth.Register;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public required string UserName { get; set; }
    public required string Name { get; set; }
}