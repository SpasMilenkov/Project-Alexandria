namespace Alexandria.Workers.MediaMetadata.Services;

public static class MimeTypeExtensions
{
    /// <summary>
    /// Maps a MIME type to the file extension used for the staged audio file.
    /// Falls back to <c>.bin</c> for unknown types.
    /// </summary>
    public static string GetExtension(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return ".bin";

        return mimeType.ToLowerInvariant() switch
        {
            "audio/mpeg" or "audio/mp3" or "audio/mpg" => ".mp3",
            "audio/wav" or "audio/x-wav" or "audio/vnd.wave" => ".wav",
            "audio/ogg" or "application/ogg" => ".ogg",
            "audio/flac" or "audio/x-flac" => ".flac",
            "audio/aac" or "audio/aacp" or "audio/x-aac" => ".aac",
            "audio/x-m4a" or "audio/mp4" or "audio/m4a" or "audio/x-m4a" => ".m4a",
            "audio/webm" => ".webm",
            "audio/wma" or "audio/x-ms-wma" => ".wma",
            "audio/opus" => ".opus",
            "audio/amr" => ".amr",
            "audio/midi" or "audio/x-midi" => ".mid",
            _ => ".bin"
        };
    }
}