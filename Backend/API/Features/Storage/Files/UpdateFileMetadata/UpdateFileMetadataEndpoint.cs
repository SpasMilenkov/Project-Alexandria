using System.Security.Claims;
using Common.Services;
using FastEndpoints;
using FluentValidation;
using Models;

namespace API.Features.Storage.Files.UpdateFileMetadata;

sealed class UpdateFileMetadataRequest
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public bool? HasPreview { get; set; }
}

sealed class UpdateFileMetadataRequestValidator : Validator<UpdateFileMetadataRequest>
{
    public UpdateFileMetadataRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("File ID cannot be empty.");

        RuleFor(x => x)
            .Must(HaveAtLeastOnePropertyToUpdate)
            .WithMessage("At least one field must be provided to update.");

        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name!)
                .NotEmpty()
                .MaximumLength(ValidationConstants.StringLengths.MediumString)
                .WithMessage("Name must be between 1 and 255 characters.");
        });
    }

    private static bool HaveAtLeastOnePropertyToUpdate(UpdateFileMetadataRequest x)
        => x.Name is not null || x.HasPreview is not null;
}

sealed class UpdateFileMetadataResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool HasPreview { get; set; }
    public DateTime? PreviewGeneratedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
}

sealed class UpdateFileMetadataEndpoint(
    IFileService fileService
) : Endpoint<UpdateFileMetadataRequest, UpdateFileMetadataResponse>
{
    public override void Configure()
    {
        Patch("/files/{id}/metadata");
        AllowAnonymous();
        Description(x => x.WithTags("Files"));

        Summary(s =>
        {
            s.Summary = "Update file metadata";
            s.Description = "Updates file metadata such as name and preview status.";
            s.Responses[200] = "File metadata updated successfully";
            s.Responses[404] = "File not found";
            s.Responses[400] = "Bad request - validation error";
            s.Responses[500] = "Internal server error";
        });
    }

    public override async Task HandleAsync(UpdateFileMetadataRequest req, CancellationToken ct)
    {
        try
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("sub")?.Value
                               ?? throw new UnauthorizedAccessException("User ID not found in token");
            var userId = Guid.Parse(userIdString);

            var updatedFile = await fileService.UpdateFileMetadata(
                req.Id,
                userId,
                req.Name,
                req.HasPreview,
                ct);

            await Send.OkAsync(new UpdateFileMetadataResponse
            {
                Id = updatedFile.Id,
                Name = updatedFile.Name,
                HasPreview = updatedFile.HasPreview,
                PreviewGeneratedAt = updatedFile.PreviewGeneratedAt,
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