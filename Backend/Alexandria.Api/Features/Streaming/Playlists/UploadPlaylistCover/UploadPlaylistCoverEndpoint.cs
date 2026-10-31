using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Services;
using FastEndpoints;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using WebMultipartSection = Microsoft.AspNetCore.WebUtilities.MultipartSection;

namespace Alexandria.Api.Features.Streaming.Playlists.UploadPlaylistCover;

internal sealed class UploadPlaylistCoverResponse
{
    public required string CoverUrl { get; set; }
}

internal sealed class UploadPlaylistCoverEndpoint(IStaticAssetService staticAssetService)
    : EndpointWithoutRequest<UploadPlaylistCoverResponse>
{
    public override void Configure()
    {
        Post("/streaming/playlists/cover");
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

        Guid? playlistId = null;
        MemoryStream? fileContent = null;

        var reader = new MultipartReader(boundary, HttpContext.Request.Body);
        WebMultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync(ct)) is not null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition) ||
                !disposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;

            if (!string.IsNullOrEmpty(disposition.FileName.Value) ||
                !string.IsNullOrEmpty(disposition.FileNameStar.Value))
            {
                if (fileContent is null)
                {
                    fileContent = new MemoryStream();
                    await section.Body.CopyToAsync(fileContent, ct);
                }
                else
                {
                    await section.Body.CopyToAsync(Stream.Null, ct);
                }

                continue;
            }

            if (string.Equals(name, "playlistId", StringComparison.OrdinalIgnoreCase))
            {
                using var textReader = new StreamReader(section.Body);
                var value = await textReader.ReadToEndAsync(ct);

                if (Guid.TryParse(value.Trim(), out var parsed))
                    playlistId = parsed;
            }
        }

        if (playlistId is null || fileContent is null)
        {
            AddError("file", "A playlistId field and a single image file section are required.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var userId = User.GetUserId();

        using var coverStream = fileContent;

        try
        {
            coverStream.Position = 0;

            await staticAssetService.UploadPlaylistCoverAsync(
                coverStream, (Guid)playlistId, userId, ct);
        }
        catch (PlaylistNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        catch (InvalidOperationException ex)
        {
            await FailAsync(ex.Message, ct);
            return;
        }

        await Send.OkAsync(new UploadPlaylistCoverResponse
        {
            CoverUrl = $"/covers/{playlistId}"
        }, ct);
    }

    private async Task FailAsync(string message, CancellationToken ct)
    {
        AddError(message);
        await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
    }
}