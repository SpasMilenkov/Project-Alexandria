using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;
using FluentValidation;

internal sealed class RestoreDirectoriesRequest
{
    public required Guid[] DirectoryIds { get; set; }
}

sealed class RestoreDirectoriesRequestValidator : Validator<RestoreDirectoriesRequest>
{
    public RestoreDirectoriesRequestValidator()
    {
        RuleFor(x => x.DirectoryIds)
            .NotNull()
            .NotEmpty()
            .WithMessage("At least one file must be specified.");

        RuleForEach(x => x.DirectoryIds)
            .NotEmpty()
            .WithMessage("File ID cannot be empty.");

        RuleFor(x => x.DirectoryIds)
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithMessage("File IDs must be unique.");
    }
}

sealed class RestoreDirectoriesEndpoint(IDirectoryService directoryService) : Endpoint<RestoreDirectoriesRequest, int>
{
    public override void Configure()
    {
        Post("directories/restore");
    }

    public override async Task HandleAsync(RestoreDirectoriesRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await Send.OkAsync(await directoryService.RestoreDirectories(req.DirectoryIds, userId, ct), ct);
    }
}
