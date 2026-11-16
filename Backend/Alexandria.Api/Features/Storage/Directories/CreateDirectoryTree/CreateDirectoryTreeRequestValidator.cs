using Alexandria.Common.Validation;
using Alexandria.Data.Models;
using FastEndpoints;
using FluentValidation;

namespace Alexandria.Api.Features.Storage.Directories.CreateDirectoryTree;

public class CreateDirectoryTreeRequestValidator
    : Validator<CreateDirectoryTreeRequest>
{
    public CreateDirectoryTreeRequestValidator()
    {
        RuleFor(x => x.Paths)
            .NotNull()
            .NotEmpty();

        RuleForEach(x => x.Paths)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(5000)
            .Must(BeRelativePath)
            .WithMessage("Invalid path format.")
            .Must(path => FindSegmentProblem(path) is null)
            .WithMessage((_, path) => FindSegmentProblem(path) ?? string.Empty);

        RuleFor(x => x.Paths)
            .Must(p => p.Distinct().Count() == p.Count)
            .WithMessage("Duplicate paths are not allowed.");
    }

    private static bool BeRelativePath(string path)
    {
        return !path.StartsWith('/') && !path.Contains("..");
    }

    // The last segment is the file itself, which only has the file name length limit.
    // Segments are not filtered for empties because the tree builder would create an unnamed folder.
    private static string? FindSegmentProblem(string path)
    {
        var segments = path.Split('/');

        foreach (var folder in segments[..^1])
        {
            var problem = DirectoryNameRules.FindProblem(folder);
            if (problem is not null) return $"\"{path}\": {problem}";
        }

        if (segments[^1].Length > ValidationConstants.StringLengths.MediumString)
        {
            return $"\"{path}\": File name cannot exceed {ValidationConstants.StringLengths.MediumString} characters.";
        }

        return null;
    }
}
