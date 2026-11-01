namespace Alexandria.Services.Preview.Images;

internal sealed record ImageHeader(string Loader, int Width, int PageHeight, int Pages)
{
    public long FramePixels => (long)Width * PageHeight;

    public long TotalPixels => FramePixels * Pages;

    public bool IsAnimatable =>
        Pages > 1 &&
        (Loader.StartsWith("VipsForeignLoadNsgif", StringComparison.Ordinal) ||
         Loader.StartsWith("VipsForeignLoadWebp", StringComparison.Ordinal));
}
