using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Common.Services;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.RestoreFiles;

internal sealed class RestoreFilesRequest
{
    public required Guid[] FileIds { get; set; }
}

sealed class RestoreFilesRequestValidator : Validator<RestoreFilesRequest>
{
    public RestoreFilesRequestValidator()
    {
        RuleFor(x => x.FileIds)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one file must be specified.");

        RuleForEach(x => x.FileIds)
            .NotEmpty()
            .WithMessage("File ID cannot be empty.");

        RuleFor(x => x.FileIds)
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("File IDs must be unique.");
    }
}

sealed class RestoreFilesEndpoint(IFileService fileService) : Endpoint<RestoreFilesRequest, int>
{
    public override void Configure()
    {
        Post("files/restore");
    }

    public override async Task HandleAsync(RestoreFilesRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await fileService.RestoreFilesAsync(req.FileIds, userId, ct), ct);
    }
}