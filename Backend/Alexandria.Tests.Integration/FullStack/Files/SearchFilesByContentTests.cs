using System.Net;
using System.Net.Http.Json;
using Alexandria.Common;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Integration.FullStack.Files;

/// <summary>
/// Search by document text and the excerpts behind it, through the real search SQL function and
/// <c>ts_headline</c>.
/// </summary>
public class SearchFilesByContentTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string MatchesRoute(Guid fileId, string query, bool firstMatchOnly = false)
        => $"/api/files/{fileId}/content-matches?query={Uri.EscapeDataString(query)}"
           + (firstMatchOnly ? "&firstMatchOnly=true" : "");

    [Fact]
    public async Task Search_findsAFileByTheWordsInItsText()
    {
        var scan = await SeedFileWithVersionAsync(f => f.WithName("scan_0042.pdf"));
        await SeedTextAsync(scan, "Hotel invoice from Paris, total due 412 EUR");

        var body = await SearchAsync("invoice paris");

        body.TotalCount.Should().Be(1);
        body.Items.Should().ContainSingle()
            .Which.Should().Match<FileResult>(f => f.FileId == scan.Id && f.MatchedInContent);
    }

    [Fact]
    public async Task Search_prefixMatchesEveryWord_inAnyOrder()
    {
        var scan = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf"));
        await SeedTextAsync(scan, "Paris trip: three invoices and one receipt");

        var body = await SearchAsync("invoi pari");

        body.Items.Should().ContainSingle(f => f.FileId == scan.Id);
    }

    [Fact]
    public async Task Search_needsEveryContentWordInTheText()
    {
        var scan = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf"));
        await SeedTextAsync(scan, "Hotel invoice from Paris");

        var body = await SearchAsync("invoice berlin");

        body.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_ranksANameMatchAboveATextOnlyMatch()
    {
        var byText = await SeedFileWithVersionAsync(f => f.WithName("scan_0042.pdf"));
        await SeedTextAsync(byText, "Hotel invoice from Paris");
        var byName = await SeedFileWithVersionAsync(f => f.WithName("invoice paris.pdf"));

        var body = await SearchAsync("invoice paris");

        body.Items.Select(f => f.FileId).Should().Equal(byName.Id, byText.Id);
        body.Items.Select(f => f.MatchedInContent).Should().Equal(false, true);
    }

    [Fact]
    public async Task Search_listsTheTextMatch_withoutNamesThatOnlyShareLetters()
    {
        // "Kecon-Gi" shares a few trigrams with "ml kem" and nothing else; it used to outrank
        // the slides that define the term.
        await SeedFileWithVersionAsync(f => f.WithName("Kecon-Gi.mp3"));
        var byText = await SeedFileWithVersionAsync(f => f.WithName("pqc_algorithms.pptx"));
        await SeedTextAsync(byText, "Kyber was renamed to ML-KEM, the module-lattice KEM");

        var body = await SearchAsync("ML-KEM");

        body.Items.Select(f => f.FileId).Should().Equal(byText.Id);
    }

    [Fact]
    public async Task Search_ranksALongNameHoldingTheWordAboveATextMatch()
    {
        // The long name is barely similar to "paris" as a whole, but it does contain the word.
        var byName = await SeedFileWithVersionAsync(f =>
            f.WithName("2024 summer vacation photos from our trip to paris.jpg"));
        var byText = await SeedFileWithVersionAsync(f => f.WithName("scan_0042.pdf"));
        await SeedTextAsync(byText, "Hotel invoice from Paris");

        var body = await SearchAsync("paris");

        body.Items.Select(f => f.FileId).Should().Equal(byName.Id, byText.Id);
    }

    [Fact]
    public async Task Search_shortWords_onlyMatchNames()
    {
        var scan = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf"));
        await SeedTextAsync(scan, "an ox at the pa farm");

        var body = await SearchAsync("ox pa");

        body.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_neverReturnsAnotherUsersFile_byItsText()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var foreign = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf").WithOwner(otherId));
        await SeedTextAsync(foreign, "Hotel invoice from Paris");

        var body = await SearchAsync("invoice paris");

        body.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_withoutText_flagsNothing()
    {
        var named = await SeedFileWithVersionAsync(f => f.WithName("budget report.pdf"));

        var body = await SearchAsync("budget");

        body.Items.Should().ContainSingle()
            .Which.Should().Match<FileResult>(f => f.FileId == named.Id && !f.MatchedInContent);
    }

    [Fact]
    public async Task ContentMatches_returnsExcerptsWithTheMatchedWordsMarked()
    {
        var scan = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf"));
        await SeedTextAsync(scan, "Booking confirmation.\fHotel invoice from Paris, total due 412 EUR.");

        var body = await GetMatchesAsync(scan.Id, "invoice paris");

        body.Status.Should().Be(DocumentTextStatus.Extracted);
        body.IsTruncated.Should().BeFalse();
        var segments = body.Fragments.Should().ContainSingle().Which.Segments;
        segments.Where(s => s.IsMatch).Select(s => s.Text).Should().Equal("invoice", "Paris");
        string.Concat(segments.Select(s => s.Text)).Should().Contain("Hotel invoice from Paris");
        segments.Should().NotContain(s => s.Text.Contains('\f'));
    }

    [Fact]
    public async Task ContentMatches_firstMatchOnly_returnsOneExcerpt()
    {
        var scan = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf"));
        var filler = string.Join(' ', Enumerable.Repeat("lorem ipsum dolor", 200));
        await SeedTextAsync(scan, $"Hotel invoice from Paris. {filler} Second invoice. {filler} Third invoice.");

        var all = await GetMatchesAsync(scan.Id, "invoice");
        var first = await GetMatchesAsync(scan.Id, "invoice", firstMatchOnly: true);

        all.Fragments.Should().HaveCount(3);
        first.Status.Should().Be(DocumentTextStatus.Extracted);
        first.Fragments.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(all.Fragments[0]);
    }

    [Fact]
    public async Task ContentMatches_noMatch_keepsTheStatus_withoutExcerpts()
    {
        var scan = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf"));
        await SeedTextAsync(scan, "Hotel invoice from Paris", DocumentTextStatus.NoTextLayer);

        var body = await GetMatchesAsync(scan.Id, "berlin");

        body.Status.Should().Be(DocumentTextStatus.NoTextLayer);
        body.Fragments.Should().BeEmpty();
    }

    [Fact]
    public async Task ContentMatches_textNotReadYet_hasNoStatus()
    {
        var file = await SeedFileWithVersionAsync();

        var body = await GetMatchesAsync(file.Id, "invoice");

        body.Status.Should().BeNull();
        body.Fragments.Should().BeEmpty();
    }

    [Fact]
    public async Task ContentMatches_trashedFile_stillAnswers()
    {
        var scan = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf").WithDeletedAt(DateTime.UtcNow));
        await SeedTextAsync(scan, "Hotel invoice from Paris");

        var body = await GetMatchesAsync(scan.Id, "invoice");

        body.Fragments.Should().ContainSingle();
    }

    [Fact]
    public async Task ContentMatches_anotherUsersFile_isNotFound()
    {
        var (_, otherId) = await CreateOtherUserAsync();
        var foreign = await SeedFileWithVersionAsync(f => f.WithName("scan.pdf").WithOwner(otherId));
        await SeedTextAsync(foreign, "Hotel invoice from Paris");

        var response = await Auth.GetAsync(MatchesRoute(foreign.Id, "invoice"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ContentMatches_blankQuery_isRejected()
    {
        var file = await SeedFileWithVersionAsync();

        var response = await Auth.GetAsync($"/api/files/{file.Id}/content-matches?query=", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task SeedTextAsync(File file, string content,
        DocumentTextStatus status = DocumentTextStatus.Extracted)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var contentObjectId = await db.FileVersions
            .Where(v => v.Id == file.CurrentVersionId)
            .Select(v => v.ContentObjectId)
            .SingleAsync(Ct);

        await unitOfWork.DocumentText.UpsertAsync(new DocumentText
        {
            Id = Guid.NewGuid(),
            ContentObjectId = contentObjectId,
            Status = status,
            Content = content,
            CharacterCount = content.Length,
            ExtractorVersion = 1
        }, Ct);
    }

    private async Task<PaginatedResult<FileResult>> SearchAsync(string text)
    {
        var response = await Auth.GetAsync(
            $"/api/files/search?nameContains={Uri.EscapeDataString(text)}&currentPage=0&pageSize=20", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResult<FileResult>>(cancellationToken: Ct);

        return body!;
    }

    private async Task<ContentMatchesResponse> GetMatchesAsync(Guid fileId, string query,
        bool firstMatchOnly = false)
    {
        var response = await Auth.GetAsync(MatchesRoute(fileId, query, firstMatchOnly), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ContentMatchesResponse>(cancellationToken: Ct);

        return body!;
    }
}