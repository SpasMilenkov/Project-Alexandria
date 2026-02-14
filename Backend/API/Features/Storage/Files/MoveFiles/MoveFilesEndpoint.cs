using System.Security.Claims;
using Common.Services;
using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Files.MoveFiles;

sealed class MoveFilesRequest
{
    public Guid[] FileIds { get; set; }
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
    }

    public override async Task HandleAsync(MoveFilesRequest r, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);

        await fileService.MoveFilesAsync(r.FileIds, r.DestinationId, userId, ct);

        await Send.OkAsync("File moved successfully", ct);
    }
}