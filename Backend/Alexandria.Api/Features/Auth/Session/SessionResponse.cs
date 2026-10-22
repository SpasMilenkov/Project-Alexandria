using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;

namespace Alexandria.Api.Features.Auth.Session;

public class SessionResponse
{
    public required UserDto User { get; set; }
    public IList<string> UserRoles { get; set; } = [];
    public required OnboardingStep OnboardingStep { get; set; }
}
