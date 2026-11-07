namespace Alexandria.Services.Storage.Content;

/// <summary>
/// The only failure text batch uploads send to clients. Exception messages can carry bucket
/// names and storage details, so those go to the logs instead.
/// </summary>
internal static class UploadErrorMessages
{
    public const string DestinationNotFound = "The upload destination was not found.";
    public const string FailedVerification = "The file failed verification. Upload it again.";
    public const string Generic = "The file could not be saved. Try uploading it again.";
}
