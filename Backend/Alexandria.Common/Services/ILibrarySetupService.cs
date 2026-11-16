using Alexandria.Common.Exceptions;
using Alexandria.Dto.Library;

namespace Alexandria.Common.Services;

public interface ILibrarySetupService
{
    /// <summary>
    /// Finishes the onboarding tour and creates the starter folders the user picked, with their
    /// automation, in one transaction. A top-level folder that already carries the chosen name is
    /// reused, and a policy it already has is left alone.
    /// </summary>
    /// <exception cref="OnboardingStepMismatchException">The user is not on the tour step.</exception>
    Task FinishTourAsync(Guid userId, IReadOnlyList<LibraryFolderChoice> folders, CancellationToken ct = default);
}
