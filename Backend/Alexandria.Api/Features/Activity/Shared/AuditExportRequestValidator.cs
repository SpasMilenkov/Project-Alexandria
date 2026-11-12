using FluentValidation;

namespace Alexandria.Api.Features.Activity.Shared;

public abstract class AuditExportRequestValidator<T> : AuditFilterValidator<T> where T : AuditExportRequest
{
    protected AuditExportRequestValidator()
    {
        RuleFor(x => x.Format).IsInEnum();

        // A bound is a row position, so half of one cannot be matched to any row.
        RuleFor(x => x.StartId)
            .NotNull()
            .When(x => x.StartAt.HasValue)
            .WithMessage("StartAt and StartId must be sent together.");

        RuleFor(x => x.StartAt)
            .NotNull()
            .When(x => x.StartId.HasValue)
            .WithMessage("StartAt and StartId must be sent together.");

        RuleFor(x => x.EndId)
            .NotNull()
            .When(x => x.EndAt.HasValue)
            .WithMessage("EndAt and EndId must be sent together.");

        RuleFor(x => x.EndAt)
            .NotNull()
            .When(x => x.EndId.HasValue)
            .WithMessage("EndAt and EndId must be sent together.");
    }
}
