namespace Alexandria.Services.Preview.Documents.BodyText;

/// <summary>
/// The whitespace owed before the next visible character. Ordered by strength, so a line break
/// wins over a space when both come up between two words.
/// </summary>
internal enum TextSeparator
{
    None,
    Space,
    Line
}