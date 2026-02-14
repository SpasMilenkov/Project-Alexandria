using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Directories.CopyDirectory;

public class CopyDirectoryRequestValidator
    : Validator<CopyDirectoryRequest>
{
    public CopyDirectoryRequestValidator()
    {
        RuleFor(x => x.DirectoryId)
            .NotEmpty();


        RuleFor(x => x.DestinationId)
            .Must((request, destinationId) =>
                destinationId == null || request.DirectoryId != destinationId)
            .WithMessage("Source and destination cannot be the same.");
    }
}