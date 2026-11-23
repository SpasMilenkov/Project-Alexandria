using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Directories;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.Notes;
using Alexandria.Common.Notes;
using Alexandria.Common.Services;
using Alexandria.Common.Validation;
using Alexandria.Dto.Files;
using FastEndpoints;
using FluentValidation;
using DirectoryNotFoundException = Alexandria.Common.Exceptions.Directories.DirectoryNotFoundException;

namespace Alexandria.Api.Features.Notes.CreateNote;

internal sealed class CreateNoteRequest
{
    /// <summary>A folder inside Notes; the Notes folder itself when left out.</summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>Becomes the file name. "Untitled" when blank.</summary>
    public string? Title { get; set; }

    /// <summary>The note's first text, such as a link to what it is about. Empty when left out.</summary>
    public string? Content { get; set; }
}

internal sealed class CreateNoteRequestValidator : Validator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.DirectoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.DirectoryId.HasValue)
            .WithMessage("DirectoryId cannot be an empty GUID.");

        RuleFor(x => x.Title)
            .MaximumLength(DirectoryNameRules.MaxLength)
            .WithMessage($"A title can be at most {DirectoryNameRules.MaxLength} characters.");

        // Characters, not bytes, so this only catches the obvious cases early. The service checks
        // the encoded size against the same limit.
        RuleFor(x => x.Content)
            .MaximumLength(TextContentLimits.MaxBytes)
            .WithMessage("The text is too large to save.");
    }
}

/// <summary>Creates a note, making the Notes folder first if this is the owner's first note.</summary>
internal sealed class CreateNoteEndpoint(INoteService noteService, IPolicyDispatcher policyDispatcher)
    : Endpoint<CreateNoteRequest, FileContentSaveResult>
{
    public override void Configure()
    {
        Post("/notes");
        Policies(Common.Auth.Policies.CanUpload);
    }

    public override async Task HandleAsync(CreateNoteRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        FileContentSaveResult created;

        try
        {
            created = await noteService.CreateNoteAsync(userId, req.DirectoryId, req.Title, req.Content, ct);
        }
        // The shared folder ownership check throws the System.IO type, the notes service its own.
        catch (Exception ex) when (ex is DirectoryNotFoundException or System.IO.DirectoryNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        catch (NotesFolderRequiredException ex)
        {
            AddError(r => r.DirectoryId, ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);
            return;
        }
        catch (FileNameTakenException ex)
        {
            AddError(r => r.Title, ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
            return;
        }
        catch (FileContentNotEditableException ex)
        {
            AddError(r => r.Content, ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);
            return;
        }

        await policyDispatcher.DispatchAsync(new FileFinalizedEvent(created.FileId,
            created.VersionId,
            created.DirectoryId,
            created.MimeType,
            created.FileName,
            userId,
            IsNewVersion: false), ct);

        await Send.OkAsync(created, ct);
    }
}
