using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Services;
using FastEndpoints;

namespace Alexandria.Api.Features.Storage.Files.GetVersionContent;

internal sealed class GetVersionContentRequest
{
    public Guid Id { get; set; }
}

/// <summary>
/// A version's text for the note editor. Unlike the download endpoints it is not audited as a
/// download, since opening a note to read it isn't taking a copy of it.
/// </summary>
internal sealed class GetVersionContentEndpoint(IContentStorageService storage)
    : Endpoint<GetVersionContentRequest>
{
    // The browser cache is shared by every account signed in on it, so note text never goes in
    // there. The editor keeps the text it opened in memory instead.
    private const string NoStore = "no-store";

    public override void Configure()
    {
        Get("/files/versions/{id}/content");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(GetVersionContentRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        try
        {
            var text = await storage.ReadVersionTextAsync(req.Id, userId, ct);

            HttpContext.Response.Headers.CacheControl = NoStore;

            await Send.BytesAsync(text.Content, contentType: $"{text.MimeType}; charset=utf-8", cancellation: ct);
        }
        catch (FileVersionNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (FileContentNotEditableException ex)
        {
            AddError(r => r.Id, ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);
        }
    }
}
