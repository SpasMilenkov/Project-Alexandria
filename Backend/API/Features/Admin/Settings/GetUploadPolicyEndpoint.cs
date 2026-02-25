using Common.Services;
using Common.Settings;
using Common.Settings.Extensions;
using FastEndpoints;

namespace API.Features.Admin.Settings
{
    public class GetUploadPolicyEndpoint : EndpointWithoutRequest<UploadPolicyResponse>
    {
        public IAdminSettingsService SettingsService { get; set; } = default!;

        public override void Configure()
        {
            Get("/admin/settings/upload-policy");
            Policies(Common.Auth.Policies.RequireAdmin);
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var value = await SettingsService.GetUploadPolicyAsync(ct);
            await Send.OkAsync(UploadPolicyMappings.ToResponse(value), ct);
        }
    }
}
