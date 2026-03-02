using API.Features.Auth.Extensions;
using Common.Services;
using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Files.MoveFiles;

internal sealed class MoveFilesRequest
{
    public required Guid[] FileIds { get; set; }
    public Guid? DestinationId { get; set; }
}

sealed class MoveFilesRequestValidator : Validator<MoveFilesRequest>
{
    public MoveFilesRequestValidator()
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

sealed class MoveFilesEndpoint(IFileService fileService) : Endpoint<MoveFilesRequest>
{
    public override void Configure()
    {
        Post("files/move");
        Policies(Common.Auth.Policies.RequireUser);
    }

    public override async Task HandleAsync(MoveFilesRequest r, CancellationToken ct)
    {
        var userId = User.GetUserId();

        await fileService.MoveFilesAsync(r.FileIds, r.DestinationId, userId, ct);

        await Send.OkAsync("File moved successfully", ct);
    }
}