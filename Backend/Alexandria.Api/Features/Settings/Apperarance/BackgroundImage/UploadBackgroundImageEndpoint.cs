using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Alexandria.Api.Features.Settings.Apperarance.BackgroundImage;

public class UploadBackgroundImageEndpoint(
    IStaticAssetService staticAssetService,
    IUserSettingsService userSettingsService,
    ILogger<UploadBackgroundImageEndpoint> logger) : EndpointWithoutRequest<GetAppearanceResponse>
{
    public override void Configure()
    {
        Post("/settings/appearance/background-image");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(HttpContext.Request.ContentType) ||
            !HttpContext.Request.ContentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            AddError("file", "Request must be multipart.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var boundary = HeaderUtilities.RemoveQuotes(
            MediaTypeHeaderValue.Parse(HttpContext.Request.ContentType).Boundary).Value;

        if (string.IsNullOrWhiteSpace(boundary))
        {
            AddError("file", "Multipart boundary is missing.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var reader = new MultipartReader(boundary, HttpContext.Request.Body);
        var section = await reader.ReadNextSectionAsync(ct);

        var hasFile = false;

        if (section is not null &&
            ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) &&
            disposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
        {
            hasFile = !string.IsNullOrEmpty(disposition.FileName.Value) ||
                      !string.IsNullOrEmpty(disposition.FileNameStar.Value);
        }

        if (!hasFile)
        {
            AddError("file", "A single image file section is required.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var userId = User.GetUserId();

        string newKey;
        try
        {
            newKey = await staticAssetService.UploadBackgroundImageAsync(
                section.Body, userId, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError("file", ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var settings = await userSettingsService.GetAppearanceAsync(userId, ct);
        var oldKey = settings.BackgroundImageKey;

        settings.BackgroundImageKey = newKey;
        settings.BackgroundImageUpdatedAt = DateTime.UtcNow;
        await userSettingsService.SetAppearanceAsync(userId, settings, userId, ct);

        if (oldKey is not null && oldKey != newKey)
        {
            try
            {
                await staticAssetService.DeleteBackgroundImageAsync(oldKey, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete replaced background image: {ObjectKey}", oldKey);
            }
        }

        var saved = await userSettingsService.GetAppearanceAsync(userId, ct);
        await Send.OkAsync(GetAppearanceResponse.FromValue(saved), ct);
    }
}