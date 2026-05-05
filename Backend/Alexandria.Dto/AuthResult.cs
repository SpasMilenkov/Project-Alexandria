using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Dto;

public class AuthResult
{
    public bool Succeeded { get; set; }
    public ApplicationUser? User { get; set; }
    public IList<string> UserRoles { get; set; }
    public string? RefreshToken { get; set; }
    public OnboardingStep OnboardingStep { get; set; }

    public static AuthResult SetSuccess(ApplicationUser user)
    {
        return new AuthResult { Succeeded = true, User = user };
    }

    public static AuthResult Failed()
    {
        return new AuthResult { Succeeded = false };
    }
}