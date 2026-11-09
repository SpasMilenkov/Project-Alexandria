using System.Net;
using System.Net.Http.Json;
using Alexandria.Common;
using Alexandria.Common.Helpers;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class GetDocumentMetadataTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Pdf = "application/pdf";
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Epub = "application/epub+zip";

    private static string Route(Guid fileId) => $"/api/files/{fileId}/document-metadata";

    private async Task UpsertAsync(Guid versionId, string? author, int extractorVersion = 1)
    {
        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.DocumentMetadata.UpsertAsync(new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            Title = "Quarterly report",
            Author = author,
            Application = "Writer",
            Producer = "LibreOffice 24.2",
            AuthoredAt = new DateTime(2026, 1, 8, 18, 26, 20, DateTimeKind.Utc),
            AuthoredOffsetMinutes = 120,
            PageCount = 3,
            PaperSize = "A4",
            RawJson = "{\"pdf:Title\":\"Quarterly report\"}",
            ExtractorVersion = extractorVersion
        }, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SeedExtraVersionAsync(Guid fileId, int versionNumber)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var content = new ContentObjectBuilder().Build();
        var version = new FileVersionBuilder()
            .WithContentObject(content)
            .WithCreatedBy(UserId)
            .WithVersionNumber(versionNumber)
            .WithFileId(fileId)
            .WithMimeType(Pdf)
            .Build();

        db.ContentObjects.Add(content);
        db.FileVersions.Add(version);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return version.Id;
    }

    private async Task<DocumentMetadataResponse> GetAsync(string route)
    {
        var response = await Auth.GetAsync(route, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<DocumentMetadataResponse>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!;
    }

    [Fact]
    public async Task Owner_GetsCurrentVersionMetadata()
    {
        var file = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pdf));
        await UpsertAsync(file.CurrentVersionId!.Value, author: "Ana Doe");

        var body = await GetAsync(Route(file.Id));

        body.FileVersionId.Should().Be(file.CurrentVersionId.Value);
        body.IsExtracted.Should().BeTrue();
        body.HasPersonalDetails.Should().BeTrue();
        body.Author.Should().Be("Ana Doe");
        body.Title.Should().Be("Quarterly report");
        body.AuthoredOffsetMinutes.Should().Be(120);
        body.PageCount.Should().Be(3);
        body.PaperSize.Should().Be("A4");
    }

    [Fact]
    public async Task Book_ReturnsBookFields_AndItsAuthorIsNotPersonal()
    {
        var file = await SeedFileWithVersionAsync(configure: f => f.WithMimeType(Epub),
            configureVersion: v => v.WithMimeType(Epub));

        using (var scope = Factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.DocumentMetadata.UpsertAsync(new DocumentMetadata
            {
                Id = Guid.NewGuid(),
                FileVersionId = file.CurrentVersionId!.Value,
                Title = "Foundation and Empire",
                Author = "Isaac Asimov",
                Publisher = "Gnome Press",
                Isbn = "9780804429573",
                Series = "Foundation",
                SeriesIndex = 2.5m,
                IsDrmProtected = true,
                RawJson = "{}",
                ExtractorVersion = 1
            }, TestContext.Current.CancellationToken);
        }

        var body = await GetAsync(Route(file.Id));

        body.Author.Should().Be("Isaac Asimov");
        body.HasPersonalDetails.Should().BeFalse();
        body.Publisher.Should().Be("Gnome Press");
        body.Isbn.Should().Be("9780804429573");
        body.Series.Should().Be("Foundation");
        body.SeriesIndex.Should().Be(2.5m);
        body.IsDrmProtected.Should().BeTrue();
        body.IsPasswordProtected.Should().BeFalse();
    }

    [Fact]
    public async Task VersionNotReadYet_ReportsNotExtracted()
    {
        var file = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pdf));

        var body = await GetAsync(Route(file.Id));

        body.IsExtracted.Should().BeFalse();
        body.HasPersonalDetails.Should().BeFalse();
        body.IsPasswordProtected.Should().BeFalse();
    }

    [Fact]
    public async Task PinnedVersion_ReportsItsOwnAuthor()
    {
        var file = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pdf));
        var scrubbedVersionId = await SeedExtraVersionAsync(file.Id, versionNumber: 2);
        await UpsertAsync(file.CurrentVersionId!.Value, author: "Ana Doe");
        await UpsertAsync(scrubbedVersionId, author: null);

        var withAuthor = await GetAsync($"{Route(file.Id)}?versionId={file.CurrentVersionId}");
        var withoutAuthor = await GetAsync($"{Route(file.Id)}?versionId={scrubbedVersionId}");

        withAuthor.HasPersonalDetails.Should().BeTrue();
        withoutAuthor.HasPersonalDetails.Should().BeFalse();
    }

    [Fact]
    public async Task VersionFromAnotherFile_Returns404()
    {
        var file = await SeedFileWithVersionAsync();
        var other = await SeedFileWithVersionAsync();

        var response = await Auth.GetAsync(
            $"{Route(file.Id)}?versionId={other.CurrentVersionId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OtherUser_Returns404()
    {
        var file = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pdf));
        await UpsertAsync(file.CurrentVersionId!.Value, author: "Ana Doe");
        var (client, _) = await CreateOtherUserAsync();

        var response = await client.GetAsync(Route(file.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var file = await SeedFileWithVersionAsync();

        var response = await Anon.GetAsync(Route(file.Id), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upsert_ReplacesTheRowForAVersion()
    {
        var file = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pdf));
        var versionId = file.CurrentVersionId!.Value;
        await UpsertAsync(versionId, author: "Ana Doe");

        await UpsertAsync(versionId, author: null, extractorVersion: 2);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var rows = await db.DocumentMetadata
            .Where(d => d.FileVersionId == versionId)
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle();
        rows[0].Author.Should().BeNull();
        rows[0].ExtractorVersion.Should().Be(2);
        rows[0].UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Candidates_SkipWhatTheBackfillMustNotRead()
    {
        var wanted = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Docx));
        var outdated = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pdf));
        var current = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pdf));
        var excluded = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pdf));
        var encrypted = await SeedFileWithVersionAsync(
            configureVersion: v => v
                .WithMimeType(Pdf)
                .WithIsEncrypted(true)
                .WithEncryptionIv(new byte[12])
                .WithEncryptionSalt(new byte[16])
                .WithIntegrityTag(new byte[16]));
        var legacy = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType("application/msword"));
        var image = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType("image/jpeg"));

        await UpsertAsync(outdated.CurrentVersionId!.Value, author: null, extractorVersion: 1);
        await UpsertAsync(current.CurrentVersionId!.Value, author: null, extractorVersion: 2);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await db.ContentObjects.ExecuteUpdateAsync(
            setters => setters.SetProperty(c => c.IsPromoted, true),
            TestContext.Current.CancellationToken);

        var unpromoted = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pdf));

        var candidates = await unitOfWork.DocumentMetadata.GetExtractionCandidatesAsync(
            DocumentMetadataFileTypes.Supported.Types,
            extractorVersion: 2,
            limit: 50,
            excludedVersionIds: [excluded.CurrentVersionId!.Value],
            TestContext.Current.CancellationToken);

        candidates.Should().BeEquivalentTo([
            new DocumentMetadataCandidate(wanted.CurrentVersionId!.Value, Docx),
            new DocumentMetadataCandidate(outdated.CurrentVersionId.Value, Pdf)
        ]);
        var candidateIds = candidates.Select(c => c.VersionId).ToList();
        candidateIds.Should().NotContain(encrypted.CurrentVersionId!.Value);
        candidateIds.Should().NotContain(legacy.CurrentVersionId!.Value);
        candidateIds.Should().NotContain(image.CurrentVersionId!.Value);
        candidateIds.Should().NotContain(unpromoted.CurrentVersionId!.Value);
    }
}
