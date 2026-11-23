using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Notes;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.SaveFileContent;

internal sealed class SaveFileContentRequest
{
    public Guid Id { get; set; }

    /// <summary>The version the edit started from. A save against any other version is refused.</summary>
    public Guid ParentVersionId { get; set; }

    /// <summary>
    /// The draft this save settles, or null when the editor has none. A draft at another revision
    /// refuses the save, so another tab's draft is never dropped unseen.
    /// </summary>
    public Guid? DraftRevision { get; set; }

    public string Content { get; set; } = string.Empty;
}

internal sealed class SaveFileContentRequestValidator : Validator<SaveFileContentRequest>
{
    public SaveFileContentRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("A file must be given.");

        RuleFor(x => x.ParentVersionId)
            .NotEqual(Guid.Empty)
            .WithMessage("The version the edit started from must be given.");

        // Characters, not bytes, so this only catches the obvious cases early. The service checks
        // the encoded size against the same limit.
        RuleFor(x => x.Content)
            .NotNull()
            .MaximumLength(TextContentLimits.MaxBytes)
            .WithMessage("The text is too large to save.");
    }
}

/// <summary>
/// A checkpoint of the note editor: saves its text as a new version of the file, addressed by
/// id, and removes the draft it came from. Runs the same folder policies an upload of a new
/// version would.
/// </summary>
internal sealed class SaveFileContentEndpoint(IFileDraftService drafts)
    : Endpoint<SaveFileContentRequest, FileContentSaveResult>
{
    public override void Configure()
    {
        Put("/files/{id}/content");
        Policies(Common.Auth.Policies.CanUpload);
    }

    public override async Task HandleAsync(SaveFileContentRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        FileContentSaveResult saved;

        try
        {
            saved = await drafts.CheckpointAsync(req.Id, req.ParentVersionId, req.DraftRevision, req.Content,
                userId, ct);
        }
        catch (FileVersionConflictException)
        {
            AddError(r => r.ParentVersionId, "This file changed since the edit started.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
            return;
        }
        catch (FileDraftConflictException)
        {
            AddError(r => r.DraftRevision, "The draft was changed somewhere else.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
            return;
        }
        catch (Exception ex) when (ex is FileRecordNotFoundException or FileVersionNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        catch (FileContentNotEditableException ex)
        {
            AddError(r => r.Content, ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);
            return;
        }

        await Send.OkAsync(saved, ct);
    }
}
