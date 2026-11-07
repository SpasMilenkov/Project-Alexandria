namespace Alexandria.Services.Storage.Content;

/// <summary>
/// Key prefixes in the temp bucket. Signed URLs may only ever target <see cref="Incoming"/>.
/// Finalize copies the bytes to <see cref="Sealed"/> before checking them, so what passes
/// verification can't be changed afterwards by a URL that is still live.
/// </summary>
internal static class TempObjectKeys
{
    public const string Incoming = "incoming/";
    public const string Sealed = "content/";
}
