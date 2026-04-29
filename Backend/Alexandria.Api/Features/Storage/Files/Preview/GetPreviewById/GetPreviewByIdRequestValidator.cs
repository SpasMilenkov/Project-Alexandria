using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.Preview.GetPreviewById;

public class GetPreviewByIdRequestValidator : Validator<GetPreviewByIdRequest>
{
    public GetPreviewByIdRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("ID cannot be empty");
    }
}