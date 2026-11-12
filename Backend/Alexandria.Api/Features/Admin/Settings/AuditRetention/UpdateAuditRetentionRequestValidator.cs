using Alexandria.Common.Settings.Values;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Admin.Settings.AuditRetention;

sealed class UpdateAuditRetentionRequestValidator : Validator<UpdateAuditRetentionRequest>
{
    public UpdateAuditRetentionRequestValidator()
    {
        RuleFor(x => x.RetentionDays)
            .InclusiveBetween(AuditRetentionValue.MinDays, AuditRetentionValue.MaxDays);
    }
}
