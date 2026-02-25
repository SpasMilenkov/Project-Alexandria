using API.Features.Auth.Extensions;
using Common.Services;
using Common.Settings;
using Common.Settings.Extensions;
using FastEndpoints;

namespace API.Features.Admin.Settings;

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
