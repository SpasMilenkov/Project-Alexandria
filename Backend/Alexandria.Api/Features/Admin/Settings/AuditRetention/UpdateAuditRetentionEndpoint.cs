using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Settings.Values;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Settings.AuditRetention;

sealed class UpdateAuditRetentionEndpoint(IAdminSettingsService settings, IUnitOfWork unitOfWork)
    : Endpoint<UpdateAuditRetentionRequest, AuditRetentionResponse>
{
    public override void Configure()
    {
        Put("/admin/settings/audit-retention");
        Policies(Common.Auth.Policies.RequireAdmin);
        Summary(s => s.Summary = "How many days audit rows are kept before the daily purge removes them");
    }

    public override async Task HandleAsync(UpdateAuditRetentionRequest req, CancellationToken ct)
    {
        await settings.SetAuditRetentionAsync(
            new AuditRetentionValue { RetentionDays = req.RetentionDays }, User.GetUserId(), ct);

        await Send.OkAsync(await AuditRetentionResponseFactory.BuildAsync(settings, unitOfWork, ct), ct);
    }
}
