using Alexandria.Common;
using Alexandria.Common.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;

namespace Alexandria.Api.Features.Admin.Settings.AuditRetention;

sealed class GetAuditRetentionEndpoint(IAdminSettingsService settings, IUnitOfWork unitOfWork)
    : EndpointWithoutRequest<AuditRetentionResponse>
{
    public override void Configure()
    {
        Get("/admin/settings/audit-retention");
        Policies(Common.Auth.Policies.RequireAdmin);
        ResponseCache(0, ResponseCacheLocation.None, noStore: true);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await AuditRetentionResponseFactory.BuildAsync(settings, unitOfWork, ct), ct);
    }
}
