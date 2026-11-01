namespace Alexandria.Common.Exceptions.Preview.Images;

/// <summary>
/// Thrown when an image header declares more pixels than the configured limit. The image is
/// rejected before any pixel data is decoded.
/// </summary>
public class ImageTooLargeException : Exception
{
    public int Width { get; }
    public int Height { get; }
    public long MaxPixels { get; }

    public ImageTooLargeException(int width, int height, long maxPixels)
        : base($"Image dimensions {width}x{height} exceed the preview limit of {maxPixels} pixels.")
    {
        Width = width;
        Height = height;
        MaxPixels = maxPixels;
    }
}
