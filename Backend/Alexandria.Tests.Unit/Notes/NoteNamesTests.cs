using Alexandria.Services.Storage.Notes;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Notes;

public class NoteNamesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("///")]
    [InlineData(".md")]
    public void CleanTitle_nothingUsable_fallsBackToUntitled(string? title)
    {
        NoteNames.CleanTitle(title).Should().Be(NoteNames.DefaultTitle);
    }

    [Theory]
    [InlineData("  Trip   plans  ", "Trip plans")]
    [InlineData("a/b\\c:d", "a b c d")]
    [InlineData("Ideas.md", "Ideas")]
    [InlineData("Ideas.MD", "Ideas")]
    [InlineData("Plans.", "Plans")]
    [InlineData("Tab\there", "Tab here")]
    public void CleanTitle_typedTitle_becomesASafeName(string title, string expected)
    {
        NoteNames.CleanTitle(title).Should().Be(expected);
    }

    [Fact]
    public void CleanTitle_longTitle_leavesRoomForTheExtensionAndANumber()
    {
        var cleaned = NoteNames.CleanTitle(new string('a', 400));

        (cleaned.Length + " 999.md".Length).Should().BeLessThanOrEqualTo(255);
    }

    [Fact]
    public void FirstFreeName_freeTitle_addsTheExtension()
    {
        NoteNames.FirstFreeName("Ideas", new HashSet<string>()).Should().Be("Ideas.md");
    }

    [Theory]
    [InlineData("Untitled.md", true)]
    [InlineData("untitled.MD", true)]
    [InlineData("Untitled 3.md", true)]
    [InlineData("Untitled.markdown", true)]
    [InlineData("Untitled 1.md", false)]
    [InlineData("Untitled plans.md", false)]
    [InlineData("Trip plans.md", false)]
    public void IsUntitled_onlyMatchesTheNamesNewNoteHandsOut(string fileName, bool expected)
    {
        NoteNames.IsUntitled(fileName).Should().Be(expected);
    }

    [Theory]
    [InlineData("notes.md", ".md")]
    [InlineData("notes.markdown", ".markdown")]
    [InlineData("notes", ".md")]
    public void ExtensionOf_keepsTheFilesOwnExtension(string fileName, string expected)
    {
        NoteNames.ExtensionOf(fileName).Should().Be(expected);
    }

    [Fact]
    public void FirstFreeName_takenNames_countsUpIgnoringCase()
    {
        var taken = new HashSet<string> { "untitled.MD", "Untitled 2.md" };

        NoteNames.FirstFreeName("Untitled", taken).Should().Be("Untitled 3.md");
    }
}
