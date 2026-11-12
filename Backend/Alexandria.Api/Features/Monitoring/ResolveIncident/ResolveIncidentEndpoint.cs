using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Monitoring.ResolveIncident;

public class ResolveIncidentRequest
{
    public Guid Id { get; set; }
    public string? Note { get; set; }
}

sealed class ResolveIncidentRequestValidator : Validator<ResolveIncidentRequest>
{
    public ResolveIncidentRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Incident ID cannot be empty.");

        RuleFor(x => x.Note)
            .MaximumLength(ValidationConstants.StringLengths.LongString)
            .When(x => !string.IsNullOrEmpty(x.Note));
    }
}

sealed class ResolveIncidentEndpoint(
    IOperationalEventService service,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork)
    : Endpoint<ResolveIncidentRequest, OperationalEvent>
{
    public override void Configure()
    {
        Patch("/monitoring/events/{id}/resolve");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(ResolveIncidentRequest req, CancellationToken ct)
    {
        var resolvedBy = User.GetUserId();
        var result = await service.ResolveAsync(req.Id, resolvedBy, req.Note, ct);

        switch (result.Outcome)
        {
            case ResolveOutcome.Resolved:
                auditWriter.Record(new AuditEntry
                {
                    EventCode = AuditEventCode.IncidentResolved,
                    OperationType = OperationType.Update,
                    EntityType = EntityType.Incident,
                    EntityId = req.Id,
                    EntityName = result.Event!.Code.ToString(),
                    Metadata = new Dictionary<string, object?>
                    {
                        ["service"] = result.Event.ServiceType.ToString(),
                        ["code"] = result.Event.Code.ToString(),
                        ["severity"] = result.Event.Severity.ToString(),
                        ["note"] = req.Note,
                    },
                });
                await unitOfWork.SaveChangesAsync(ct);

                await Send.OkAsync(result.Event, ct);
                break;
            case ResolveOutcome.AlreadyResolved:
                AddError(x => x.Id, "This incident has already been resolved.");
                await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
                break;
            default:
                await Send.NotFoundAsync(ct);
                break;
        }
    }
}