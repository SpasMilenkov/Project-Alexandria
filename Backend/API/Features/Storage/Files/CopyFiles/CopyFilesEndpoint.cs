using System.Security.Claims;
using Common.Services;
using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Files.CopyFiles;

sealed class CopyFilesRequest
{
    public Guid[] FileIds { get; set; }
    public Guid DestinationId { get; set; }
}

sealed class CopyFilesRequestValidator : Validator<CopyFilesRequest>
{
    public CopyFilesRequestValidator()
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

sealed class CopyDirectoryEndpoint(IFileService fileService) : Endpoint<CopyFilesRequest>
{
    public override void Configure()
    {
        Post("/files/copy");
    }

    public override async Task HandleAsync(CopyFilesRequest req, CancellationToken ct)
    {
        var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value
                           ?? throw new UnauthorizedAccessException("User ID not found in token");
        var userId = Guid.Parse(userIdString);

        await fileService.CopyFilesAsync(req.FileIds, req.DestinationId, userId, ct);
        await Send.OkAsync(cancellation: ct);
    }
}