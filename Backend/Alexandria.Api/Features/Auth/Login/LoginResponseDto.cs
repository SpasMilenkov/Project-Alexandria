using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;

namespace Alexandria.Api.Features.Auth.Login;

public class LoginResponse
{
    public bool Success { get; set; }
    public UserDto? User { get; set; }
    public IList<string> UserRoles { get; set; } = [];
    public required OnboardingStep OnboardingStep { get; set; }
}