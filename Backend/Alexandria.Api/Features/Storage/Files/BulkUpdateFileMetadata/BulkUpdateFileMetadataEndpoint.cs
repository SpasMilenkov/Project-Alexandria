using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using Alexandria.Common.Validation;
using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.BulkUpdateFileMetadata;

internal sealed class BulkUpdateFileMetadataRequest
{
    public Guid[] FileIds { get; set; } = [];
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Year { get; set; }
}

sealed class BulkUpdateFileMetadataRequestValidator : Validator<BulkUpdateFileMetadataRequest>
{
    public BulkUpdateFileMetadataRequestValidator()
    {
        RuleFor(x => x.FileIds)
            .NotEmpty()
            .WithMessage("At least one file ID must be provided.");

        RuleFor(x => x.FileIds)
            .Must(ids => ids is not null && ids.Length <= 100)
            .WithMessage("At most 100 files can be updated at once.");

        RuleFor(x => x)
            .Must(x => x.Title is not null || x.Artist is not null
                                           || x.Album is not null || x.Year is not null)
            .WithMessage("At least one metadata field must be provided.");

        When(x => x.Title is not null, () =>
        {
            RuleFor(x => x.Title!)
                .NotEmpty()
                .MaximumLength(ValidationConstants.StringLengths.MediumString);
        });

        When(x => x.Artist is not null, () =>
        {
            RuleFor(x => x.Artist!)
                .NotEmpty()
                .MaximumLength(ValidationConstants.StringLengths.MediumString);
        });

        When(x => x.Album is not null, () =>
        {
            RuleFor(x => x.Album!)
                .NotEmpty()
                .MaximumLength(ValidationConstants.StringLengths.MediumString);
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

internal sealed class BulkUpdateFileMetadataItemResult
{
    public Guid FileId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

internal sealed class BulkUpdateFileMetadataResponse
{
    public List<BulkUpdateFileMetadataItemResult> Results { get; set; } = [];
}

sealed class BulkUpdateFileMetadataEndpoint(IFileService fileService)
    : Endpoint<BulkUpdateFileMetadataRequest, BulkUpdateFileMetadataResponse>
{
    public override void Configure()
    {
        Patch("/files/metadata/bulk");
        Policies(Common.Auth.Policies.RequireUser);
        Description(x => x.WithTags("Files"));

        Summary(s =>
        {
            s.Summary = "Bulk update file metadata";
            s.Description = "Applies the same partial metadata update (title, artist, album, year) "
                            + "to each file independently. Files owned by someone else are reported as "
                            + "failures rather than aborting the batch.";
            s.Responses[200] = "Batch processed, see per-item results";
            s.Responses[400] = "Bad request - validation error";
        });
    }

    public override async Task HandleAsync(BulkUpdateFileMetadataRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var results = await fileService.BulkUpdateFileMetadataAsync(
            req.FileIds,
            userId,
            req.Title,
            req.Artist,
            req.Album,
            req.Year,
            ct);

        await Send.OkAsync(new BulkUpdateFileMetadataResponse
        {
            Results = results
                .Select(r => new BulkUpdateFileMetadataItemResult
                {
                    FileId = r.FileId,
                    Success = r.Success,
                    Error = r.Error,
                })
                .ToList(),
        }, ct);
    }
}