using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Activity.Shared;

public abstract class AuditFilterValidator<T> : Validator<T> where T : AuditFilterRequest
{
    protected AuditFilterValidator()
    {
        RuleFor(x => x.EntityType).IsInEnum().When(x => x.EntityType.HasValue);
        RuleFor(x => x.OperationType).IsInEnum().When(x => x.OperationType.HasValue);
        RuleFor(x => x.Source).IsInEnum().When(x => x.Source.HasValue);
        RuleForEach(x => x.EventCodes).IsInEnum();
        RuleFor(x => x.EventCodes!.Length).LessThanOrEqualTo(50).When(x => x.EventCodes is not null);
        RuleFor(x => x.IpAddress).MaximumLength(ValidationConstants.StringLengths.IpAddress);

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("To must not be earlier than From.");
    }
}
