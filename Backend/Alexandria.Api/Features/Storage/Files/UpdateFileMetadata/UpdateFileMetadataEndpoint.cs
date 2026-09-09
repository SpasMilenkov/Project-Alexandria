using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common;
using Alexandria.Common.Services;
using Alexandria.Common.Validation;
using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.UpdateFileMetadata;

internal sealed class UpdateFileMetadataRequest
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Year { get; set; }
}

sealed class UpdateFileMetadataRequestValidator : Validator<UpdateFileMetadataRequest>
{
    public UpdateFileMetadataRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("File ID cannot be empty.");

        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name!)
                .NotEmpty()
                .MaximumLength(ValidationConstants.StringLengths.MediumString)
                .WithMessage("Name must be between 1 and 255 characters.");
        });

        When(x => x.Title is not null, () =>
        {
            RuleFor(x => x.Title!)
                .NotEmpty()
                .MaximumLength(ValidationConstants.StringLengths.MediumString)
                .WithMessage("Title must be between 1 and 255 characters.");
        });

        When(x => x.Artist is not null, () =>
        {
            RuleFor(x => x.Artist!)
                .NotEmpty()
                .MaximumLength(ValidationConstants.StringLengths.MediumString)
                .WithMessage("Artist must be between 1 and 255 characters.");
        });

        When(x => x.Album is not null, () =>
        {
            RuleFor(x => x.Album!)
                .NotEmpty()
                .MaximumLength(ValidationConstants.StringLengths.MediumString)
                .WithMessage("Album must be between 1 and 255 characters.");
        });

        When(x => x.Year is not null, () =>
        {
            RuleFor(x => x.Year!)
                .NotEmpty()
                .Must(MetadataValidation.IsValidYear)
                .WithMessage("Year must be a 4-digit year (e.g. 1999).");
        });
    }
}

internal sealed class UpdateFileMetadataResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Year { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
}

sealed class UpdateFileMetadataEndpoint(
    IFileService fileService,
    IUnitOfWork unitOfWork
) : Endpoint<UpdateFileMetadataRequest, UpdateFileMetadataResponse>
{
    public override void Configure()
    {
        Patch("/files/{id}/metadata");
        Policies(Common.Auth.Policies.RequireUser);
        Description(x => x.WithTags("Files"));

        Summary(s =>
        {
            s.Summary = "Update file metadata";
            s.Description = "Updates file metadata such as name, title, artist, album and year.";
            s.Responses[200] = "File metadata updated successfully";
            s.Responses[404] = "File not found";
            s.Responses[403] = "Forbidden - not the file owner";
            s.Responses[400] = "Bad request - validation error";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync(UpdateFileMetadataRequest req, CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();

            var file = await unitOfWork.Files.GetByIdAsync(req.Id, ct);
            if (file is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var isOwner = file.OwnerId == userId;
            var isAdmin = User.IsInRole(Common.Seeding.Roles.Admin);
            if (!isOwner && !isAdmin)
            {
                await Send.ForbiddenAsync(ct);
                return;
            }

            var updatedFile = await fileService.UpdateFileMetadataAsync(
                req.Id,
                userId,
                req.Name,
                req.Title,
                req.Artist,
                req.Album,
                req.Year,
                ct);

            await Send.OkAsync(new UpdateFileMetadataResponse
            {
                Id = updatedFile.Id,
                Name = updatedFile.Name,
                Title = updatedFile.MediaMetadata?.Title,
                Artist = updatedFile.MediaMetadata?.Artist,
                Album = updatedFile.MediaMetadata?.Album,
                Year = updatedFile.MediaMetadata?.Year,
                UpdatedAt = updatedFile.UpdatedAt,
                UpdatedBy = updatedFile.UpdatedBy ?? userId
            }, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            await Send.NotFoundAsync(ct);
        }
        catch (Exception ex)
        {
            ThrowError($"Update failed: {ex.Message}");
        }
    }
}