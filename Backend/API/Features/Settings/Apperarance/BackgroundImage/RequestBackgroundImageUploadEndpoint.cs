using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;

namespace API.Features.Settings.Apperarance.BackgroundImage;

public class RequestUploadResponse
{
    public string UploadUrl { get; set; } = default!;
    public string ObjectKey { get; set; } = default!;
}

public class RequestBackgroundImageUploadEndpoint(IStorageService storageService, IUserSettingsService userSettingsService) : EndpointWithoutRequest<RequestUploadResponse>
{
    public override void Configure() =>
        Post("/settings/appearance/background-image");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var settings = await userSettingsService.GetAppearanceAsync(userId, ct);

        // Reuse the existing image key if one exists so we overwrite in place
        // and don't leave orphaned objects in the bucket on every replace.
        // Generate a new opaque GUID only on first upload.
        var parts = settings.BackgroundImageKey?.Split('/');
        var imageId = parts?[^1] ?? Guid.NewGuid().ToString();
        var objectKey = $"background_images/{imageId}";

        var uploadUrl = await storageService.GenerateImageUploadUrl(
            objectKey,
            TimeSpan.FromMinutes(15));

        await Send.OkAsync(new RequestUploadResponse
        {
            UploadUrl = uploadUrl,
            ObjectKey = objectKey,
        }, ct);
    }
}
