using Alexandria.Data.Models.Enumerators;

namespace Alexandria.Common.Exceptions.Preview.Media;

/// <summary>
/// Thrown when a <see cref="FileCategory"/> that has no media preview strategy
/// is passed to the preview service.
/// </summary>
public class UnsupportedMediaCategoryException(FileCategory fileCategory)
    : Exception($"File category '{fileCategory}' is not supported for media preview.")
{
    public FileCategory FileCategory { get; } = fileCategory;
}