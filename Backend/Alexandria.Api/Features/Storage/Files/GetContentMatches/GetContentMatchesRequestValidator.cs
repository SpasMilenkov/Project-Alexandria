using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Files.GetContentMatches;

internal sealed class GetContentMatchesRequestValidator : Validator<GetContentMatchesRequest>
{
    // Matches the file search's own limit, since the query comes from the same box.
    private const int MaxQueryLength = 200;

    public GetContentMatchesRequestValidator()
    {
        RuleFor(x => x.FileId)
            .NotEqual(Guid.Empty)
            .WithMessage("FileId cannot be an empty GUID");

        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query is required")
            .MaximumLength(MaxQueryLength)
            .WithMessage($"Query cannot exceed {MaxQueryLength} characters");
    }
}