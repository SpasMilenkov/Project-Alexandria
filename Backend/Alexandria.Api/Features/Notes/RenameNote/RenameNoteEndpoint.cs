using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Services;
using Alexandria.Common.Validation;
using Alexandria.Dto.Notes;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Notes.RenameNote;

internal sealed class RenameNoteRequest
{
    public Guid FileId { get; set; }

    /// <summary>The name without its extension; the file keeps the one it has.</summary>
    public string Title { get; set; } = string.Empty;
}

internal sealed class RenameNoteRequestValidator : Validator<RenameNoteRequest>
{
    public RenameNoteRequestValidator()
    {
        RuleFor(x => x.FileId)
            .NotEqual(Guid.Empty)
            .WithMessage("A file must be given.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(DirectoryNameRules.MaxLength)
            .WithMessage($"A name must be between 1 and {DirectoryNameRules.MaxLength} characters.");
    }
}

/// <summary>
/// Renames a note from the editor. Unlike the explorer's rename it keeps the extension, cleans
/// the name the way "New note" does and refuses a name another file in the folder has.
/// </summary>
internal sealed class RenameNoteEndpoint(INoteService noteService)
    : Endpoint<RenameNoteRequest, NoteRenameResult>
{
    public override void Configure()
    {
        Patch("/notes/{fileId}/name");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(RenameNoteRequest req, CancellationToken ct)
    {
        try
        {
            var renamed = await noteService.RenameAsync(req.FileId, req.Title, User.GetUserId(), ct);

            await Send.OkAsync(renamed, ct);
        }
        catch (FileRecordNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (FileNameTakenException ex)
        {
            AddError(r => r.Title, ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
    }
}
