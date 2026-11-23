using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Notes;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.SaveFileDraft;

internal sealed class SaveFileDraftRequest
{
    public Guid Id { get; set; }

    /// <summary>The version the draft builds on. A write against any other version is refused.</summary>
    public Guid BaseVersionId { get; set; }

    /// <summary>The draft revision this editor last saw, or null when it has seen none.</summary>
    public Guid? ExpectedRevision { get; set; }

    public string Content { get; set; } = string.Empty;
}

internal sealed class SaveFileDraftRequestValidator : Validator<SaveFileDraftRequest>
{
    public SaveFileDraftRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEqual(Guid.Empty)
            .WithMessage("A file must be given.");

        RuleFor(x => x.BaseVersionId)
            .NotEqual(Guid.Empty)
            .WithMessage("The version the draft builds on must be given.");

        // Characters, not bytes, so this only catches the obvious cases early. The service checks
        // the encoded size against the same limit.
        RuleFor(x => x.Content)
            .NotNull()
            .MaximumLength(TextContentLimits.MaxBytes)
            .WithMessage("The text is too large to save.");
    }
}

/// <summary>
/// Autosave for the note editor: keeps the working text without making a version, so nothing
/// runs in the background. A checkpoint through <c>PUT /files/{id}/content</c> turns it into one.
/// </summary>
internal sealed class SaveFileDraftEndpoint(IFileDraftService drafts)
    : Endpoint<SaveFileDraftRequest, FileDraftSaveResult>
{
    public override void Configure()
    {
        Put("/files/{id}/draft");
        Policies(Common.Auth.Policies.CanUpload);
    }

    public override async Task HandleAsync(SaveFileDraftRequest req, CancellationToken ct)
    {
        FileDraftSaveResult saved;

        try
        {
            saved = await drafts.SaveAsync(req.Id, req.BaseVersionId, req.ExpectedRevision, req.Content,
                User.GetUserId(), ct);
        }
        catch (FileVersionConflictException)
        {
            AddError(r => r.BaseVersionId, "This file has a newer version than the draft builds on.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
            return;
        }
        catch (FileDraftConflictException)
        {
            AddError(r => r.ExpectedRevision, "The draft was changed somewhere else.");
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
