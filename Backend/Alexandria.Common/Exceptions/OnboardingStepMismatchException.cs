using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Exceptions;

public class OnboardingStepMismatchException(Guid userId, OnboardingStep expected)
    : InvalidOperationException($"User {userId} is not at the {expected} onboarding step.")
{
    public Guid UserId { get; } = userId;
    public OnboardingStep Expected { get; } = expected;
}
