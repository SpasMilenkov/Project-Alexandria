using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Admin.Audit.GetAccessOverview;

sealed class AccessOverviewRequestValidator : Validator<AccessOverviewRequest>
{
    // Failed sign-ins are read back over twice the window for the trend, so a fixed set keeps
    // that read bounded.
    public static readonly int[] AllowedDays = [30, 60, 90];

    public AccessOverviewRequestValidator()
    {
        RuleFor(x => x.Days)
            .Must(days => AllowedDays.Contains(days))
            .WithMessage($"Days must be one of {string.Join(", ", AllowedDays)}.");
    }
}
