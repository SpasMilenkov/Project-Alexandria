using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Directories.GetDirectoryPath;

public class GetDirectoryPathRequestValidator : Validator<GetDirectoryPathRequest>
{
    public GetDirectoryPathRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}