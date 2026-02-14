using FastEndpoints;
using FluentValidation;

namespace API.Features.Storage.Directories.CreateDirectoryTree;

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
            .Must(BeValidRelativePath)
            .WithMessage("Invalid path format.");

        RuleFor(x => x.Paths)
            .Must(p => p.Distinct().Count() == p.Count)
            .WithMessage("Duplicate paths are not allowed.");
    }

    private bool BeValidRelativePath(string path)
    {
        if (path.StartsWith("/"))
            return false;

        if (path.Contains(".."))
            return false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment.Length > 255)
                return false;

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;
        }

        return true;
    }
}