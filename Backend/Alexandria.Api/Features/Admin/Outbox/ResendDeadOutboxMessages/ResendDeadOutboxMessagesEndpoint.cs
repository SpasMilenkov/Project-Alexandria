using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Outbox.ResendDeadOutboxMessages;

public sealed record ResendDeadOutboxMessagesResponse
{
    public required int Resent { get; init; }
}

sealed class ResendDeadOutboxMessagesEndpoint(
    IUnitOfWork unitOfWork,
    IOutboxSignal signal,
    IAuditWriter auditWriter)
    : EndpointWithoutRequest<ResendDeadOutboxMessagesResponse>
{
    public override void Configure()
    {
        Post("/admin/outbox/resend-dead");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var resent = await unitOfWork.OutboxMessages.ResendDeadAsync(DateTime.UtcNow, ct);

        if (resent > 0)
        {
            auditWriter.Record(new AuditEntry
            {
                EventCode = AuditEventCode.OutboxMessagesResent,
                OperationType = OperationType.Update,
                EntityType = EntityType.System,
                Metadata = new Dictionary<string, object?> { ["resent"] = resent },
            });
            await unitOfWork.SaveChangesAsync(ct);

            signal.Notify();
        }

        await Send.OkAsync(new ResendDeadOutboxMessagesResponse { Resent = resent }, ct);
    }
}
