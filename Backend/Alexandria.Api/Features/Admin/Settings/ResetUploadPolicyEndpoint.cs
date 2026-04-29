using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Common.Settings.Extensions;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Settings;

public class ResetUploadPolicyEndpoint : EndpointWithoutRequest<UploadPolicyResponse>
{
    public IAdminSettingsService SettingsService { get; set; } = default!;

    public override void Configure()
    {
        Delete("/admin/settings/upload-policy");
        Policies(Common.Auth.Policies.RequireAdmin);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var updatedBy = User.GetUserId();
        await SettingsService.ResetUploadPolicyAsync(updatedBy, ct);

        var saved = await SettingsService.GetUploadPolicyAsync(ct);
        await Send.OkAsync(UploadPolicyMappings.ToResponse(saved), ct);
    }
}