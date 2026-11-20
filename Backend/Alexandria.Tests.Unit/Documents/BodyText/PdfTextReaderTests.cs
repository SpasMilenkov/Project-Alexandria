using Alexandria.Services.Preview.Documents.BodyText;
using AwesomeAssertions;

namespace Alexandria.Tests.Unit.Documents.BodyText;

/// <summary>
/// The fixtures match what pdftotext 26.01 printed for real two-page files: a form feed ends
/// every page, and an image-only PDF prints nothing but those.
/// </summary>
public class PdfTextReaderTests
{
    [Theory]
    [InlineData("Hotel invoice\n\n\fSecond page\n\n\f", 2)]
    [InlineData("\f\f", 2)]
    [InlineData("", 0)]
    public void CountPages_countsTheFormFeedEndingEachPage(string output, int expected)
    {
        PdfTextReader.CountPages(output).Should().Be(expected);
    }

    [Theory]
    [InlineData(2, 0, true)]
    [InlineData(3, 45, true)]
    [InlineData(3, 60, false)]
    [InlineData(1, 300, false)]
    [InlineData(0, 0, false)]
    public void LooksScanned_needsAtLeastAFewCharactersPerPage(int pages, int visible, bool expected)
    {
        PdfTextReader.LooksScanned(pages, visible).Should().Be(expected);
    }
}