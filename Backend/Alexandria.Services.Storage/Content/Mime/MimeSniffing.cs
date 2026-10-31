using Alexandria.Common.Mime;

namespace Alexandria.Services.Storage.Content.Mime;

public static class MimeSniffing
{
    /// <summary>Number of leading bytes captured from an upload for content sniffing.</summary>
    public const int HeaderBytes = 16 * 1024;

    public const string Octet = MimeAliases.Octet;
}
