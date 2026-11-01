namespace Alexandria.Common.Exceptions.Preview.Images;

/// <summary>
/// Thrown when neither libvips nor the ImageMagick fallback can decode the source image.
/// </summary>
public class UnsupportedImageFormatException : Exception
{
    public string? MimeType { get; }

    public UnsupportedImageFormatException(string? mimeType)
        : base($"Image format '{mimeType ?? "unknown"}' is not supported for previews.")
    {
        MimeType = mimeType;
    }

    public UnsupportedImageFormatException(string? mimeType, Exception inner)
        : base($"Image format '{mimeType ?? "unknown"}' could not be decoded for previews: {inner.Message}", inner)
    {
        MimeType = mimeType;
    }
}
