using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Tags.DeleteTag;

public sealed class DeleteTagRequestValidator
    : Validator<DeleteTagRequest>
{
    public DeleteTagRequestValidator()
    {
        RuleFor(x => x.TagId)
            .NotEmpty()
            .WithMessage("Tag ID cannot be empty.");
    }
}