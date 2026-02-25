using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Settings.Apperarance.BackgroundImage
{
    public class RequestUploadResponse
    {
        public string UploadUrl { get; set; } = default!;
        public string ObjectKey { get; set; } = default!;
    }

    public class RequestBackgroundImageUploadEndpoint : EndpointWithoutRequest<RequestUploadResponse>
    {
        public IStorageService StorageService { get; set; } = default!;
        public IUserSettingsService UserSettingsService { get; set; } = default!;

        public override void Configure() =>
            Post("/settings/appearance/background-image");

        public override async Task HandleAsync(CancellationToken ct)
        {
            var userId = User.GetUserId();
            var settings = await UserSettingsService.GetAppearanceAsync(userId, ct);

            // Reuse the existing image key if one exists so we overwrite in place
            // and don't leave orphaned objects in the bucket on every replace.
            // Generate a new opaque GUID only on first upload.
            var imageId = settings.BackgroundImageKey?.Split('/').Last() ?? Guid.NewGuid().ToString();
            var objectKey = $"background_images/{imageId}";

            var uploadUrl = await StorageService.GenerateImageUploadUrl(
                objectKey,
                TimeSpan.FromMinutes(15));

            await Send.OkAsync(new RequestUploadResponse
            {
                UploadUrl = uploadUrl,
                ObjectKey = objectKey,
            }, ct);
        }
    }
}
