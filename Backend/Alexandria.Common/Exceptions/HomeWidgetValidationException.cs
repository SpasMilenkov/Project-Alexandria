using System.ComponentModel.DataAnnotations;

namespace Alexandria.Common.Exceptions;

public sealed class HomeWidgetValidationException(IReadOnlyList<ValidationResult> errors, int statusCode = 422)
    : Exception("Invalid widget changes.")
{
    public IReadOnlyList<ValidationResult> Errors { get; } = errors;
    public int StatusCode { get; } = statusCode;
}
