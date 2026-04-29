using Alexandria.Common.Services;
using Alexandria.Common.Settings;
using Alexandria.Common.Settings.Extensions;
using FastEndpoints;

namespace Alexandria.Api.Features.Admin.Settings
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