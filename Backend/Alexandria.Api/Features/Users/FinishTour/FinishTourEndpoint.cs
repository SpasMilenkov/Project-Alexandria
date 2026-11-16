using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Library;
using Alexandria.Common.Services;
using Alexandria.Common.Validation;
using Alexandria.Dto.Library;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Users.FinishTour;

internal sealed class FinishTourRequest
{
    public List<LibraryFolderChoice> Folders { get; init; } = [];
}

internal sealed class FinishTourValidator : Validator<FinishTourRequest>
{
    public FinishTourValidator()
    {
        RuleFor(x => x.Folders)
            .NotNull()
            .Must(f => f.Count <= LibraryFolderPresets.All.Count)
            .WithMessage("Too many starter folders.")
            .Must(f => f.Select(c => c.Kind).Distinct().Count() == f.Count)
            .WithMessage("Each starter folder can only be chosen once.")
            .Must(f => f.Select(c => c.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == f.Count)
            .WithMessage("Starter folders need different names.");

        RuleForEach(x => x.Folders).ChildRules(folder =>
        {
            folder.RuleFor(c => c.Kind).IsInEnum();

            // Trimmed because the setup service stores the trimmed name.
            folder.RuleFor(c => c.Name)
                .Must(n => DirectoryNameRules.FindProblem(n?.Trim()) is null)
                .WithMessage((_, n) => DirectoryNameRules.FindProblem(n?.Trim()) ?? string.Empty);

            folder.RuleForEach(c => c.AudioRungs).IsInEnum();
            folder.RuleForEach(c => c.VideoRungs).IsInEnum();
        });
    }
}

internal sealed class FinishTourEndpoint(ILibrarySetupService librarySetupService) : Endpoint<FinishTourRequest>
{
    public override void Configure()
    {
        Patch("/users/finish-tour");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(FinishTourRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            await librarySetupService.FinishTourAsync(userId, req.Folders, ct);
            await Send.OkAsync(cancellation: ct);
        }
        catch (OnboardingStepMismatchException)
        {
            AddError("The tour is already finished.");
            await Send.ErrorsAsync(409, ct);
        }
    }
}
