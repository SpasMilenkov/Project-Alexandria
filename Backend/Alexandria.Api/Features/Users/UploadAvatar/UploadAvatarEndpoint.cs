using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions;
using Alexandria.Common.Services;
using FastEndpoints;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Alexandria.Api.Features.Users.UploadAvatar;

sealed class UploadAvatarEndpoint(IUserAvatarService avatarService)
    : EndpointWithoutRequest<UploadAvatarResponse>
{
    public override void Configure()
    {
        Post("/users/profile/avatar");
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

        if (section is null || !hasFile)
        {
            AddError("file", "A single image file section is required.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        try
        {
            var avatarUrl = await avatarService.ReplaceAsync(section.Body, User.GetUserId(), ct);

            await Send.OkAsync(new UploadAvatarResponse { AvatarUrl = avatarUrl }, ct);
        }
        catch (UserNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError("file", ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
        }
    }
}
