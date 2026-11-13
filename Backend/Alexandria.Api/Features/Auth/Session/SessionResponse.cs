using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Users;

namespace Alexandria.Api.Features.Auth.Session;

public class SessionResponse
{
    public required SessionUserDto User { get; set; }
    public IList<string> UserRoles { get; set; } = [];
    public required OnboardingStep OnboardingStep { get; set; }
}
