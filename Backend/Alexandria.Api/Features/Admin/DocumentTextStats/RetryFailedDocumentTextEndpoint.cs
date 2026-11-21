using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using Alexandria.Dto.DocumentTextStats;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.DocumentTextStats;

/// <summary>
/// Gives every document whose text failed a fresh set of attempts. The sweep picks them up on its
/// next cycle, so nothing is read during the request.
/// </summary>
sealed class RetryFailedDocumentTextEndpoint(
    IDocumentTextStatsService statsService,
    IAuditWriter auditWriter,
    IUnitOfWork unitOfWork)
    : EndpointWithoutRequest<RetryFailedDocumentTextResponse>
{
    public override void Configure()
    {
        Post("/admin/document-text/retry-failed");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        int reset;

        // The reset is a bulk update that commits on its own, so without the transaction a failed
        // audit save would leave the rows reset with no record of who did it.
        await unitOfWork.BeginTransactionAsync(ct);

        try
        {
            reset = await statsService.RetryFailedAsync(ct);

            if (reset > 0)
            {
                auditWriter.Record(new AuditEntry
                {
                    EventCode = AuditEventCode.DocumentTextRetried,
                    OperationType = OperationType.Update,
                    EntityType = EntityType.System,
                    // "requeued" reads as such in the activity view; "reset" there means a setting reset.
                    Metadata = new Dictionary<string, object?> { ["requeued"] = reset },
                });
            }

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }

        await Send.OkAsync(new RetryFailedDocumentTextResponse(reset), ct);
    }
}