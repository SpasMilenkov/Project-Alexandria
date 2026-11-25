using Alexandria.Common;
using Alexandria.Dto.Outbox;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.Outbox.GetOutboxSummary;

sealed class GetOutboxSummaryEndpoint(IUnitOfWork unitOfWork) : EndpointWithoutRequest<OutboxSummaryDto>
{
    private const int RecentDeadLimit = 20;

    public override void Configure()
    {
        Get("/admin/outbox");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var summary = await unitOfWork.OutboxMessages.GetSummaryAsync(RecentDeadLimit, ct);

        await Send.OkAsync(summary, ct);
    }
}
