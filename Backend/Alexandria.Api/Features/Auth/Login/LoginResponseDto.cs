using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Users;

namespace Alexandria.Api.Features.Auth.Login;

public class LoginResponse
{
    public bool Success { get; set; }
    public SessionUserDto? User { get; set; }
    public IList<string> UserRoles { get; set; } = [];
    public required OnboardingStep OnboardingStep { get; set; }
}