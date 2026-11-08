using System.Net;
using System.Net.Http.Json;
using Alexandria.Common;
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

public class GetCaptureMetadataTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static string Route(Guid fileId) => $"/api/files/{fileId}/capture-metadata";

    private async Task UpsertAsync(Guid versionId, double? latitude, int extractorVersion = 1)
    {
        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.CaptureMetadata.UpsertAsync(new CaptureMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            Width = 3024,
            Height = 4032,
            CapturedAt = new DateTime(2026, 11, 7, 18, 34, 29, DateTimeKind.Utc),
            CapturedOffsetMinutes = 120,
            Latitude = latitude,
            Longitude = latitude is null ? null : 23.3,
            CameraMake = "Apple",
            RawJson = "{\"exif-ifd0-Make\":\"Apple\"}",
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
            .Build();

        db.ContentObjects.Add(content);
        db.FileVersions.Add(version);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return version.Id;
    }

    private async Task<CaptureMetadataResponse> GetAsync(string route)
    {
        var response = await Auth.GetAsync(route, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CaptureMetadataResponse>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!;
    }

    [Fact]
    public async Task Owner_GetsCurrentVersionMetadata()
    {
        var file = await SeedFileWithVersionAsync();
        await UpsertAsync(file.CurrentVersionId!.Value, latitude: 42.68);

        var body = await GetAsync(Route(file.Id));

        body.FileVersionId.Should().Be(file.CurrentVersionId.Value);
        body.IsExtracted.Should().BeTrue();
        body.HasLocation.Should().BeTrue();
        body.Latitude.Should().Be(42.68);
        body.Width.Should().Be(3024);
        body.CameraMake.Should().Be("Apple");
        body.CapturedOffsetMinutes.Should().Be(120);
    }

    [Fact]
    public async Task VersionNotReadYet_ReportsNotExtracted()
    {
        var file = await SeedFileWithVersionAsync();

        var body = await GetAsync(Route(file.Id));

        body.IsExtracted.Should().BeFalse();
        body.HasLocation.Should().BeFalse();
    }

    [Fact]
    public async Task PinnedVersion_ReportsItsOwnLocation()
    {
        var file = await SeedFileWithVersionAsync();
        var croppedVersionId = await SeedExtraVersionAsync(file.Id, versionNumber: 2);
        await UpsertAsync(file.CurrentVersionId!.Value, latitude: 42.68);
        await UpsertAsync(croppedVersionId, latitude: null);

        var withLocation = await GetAsync($"{Route(file.Id)}?versionId={file.CurrentVersionId}");
        var withoutLocation = await GetAsync($"{Route(file.Id)}?versionId={croppedVersionId}");

        withLocation.HasLocation.Should().BeTrue();
        withoutLocation.HasLocation.Should().BeFalse();
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
        var file = await SeedFileWithVersionAsync();
        await UpsertAsync(file.CurrentVersionId!.Value, latitude: 42.68);
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
        var file = await SeedFileWithVersionAsync();
        var versionId = file.CurrentVersionId!.Value;
        await UpsertAsync(versionId, latitude: 42.68);

        await UpsertAsync(versionId, latitude: null, extractorVersion: 2);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var rows = await db.CaptureMetadata
            .Where(c => c.FileVersionId == versionId)
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle();
        rows[0].Latitude.Should().BeNull();
        rows[0].ExtractorVersion.Should().Be(2);
        rows[0].UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Candidates_SkipWhatTheBackfillMustNotRead()
    {
        var wanted = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType("image/heic"));
        var outdated = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType("image/jpeg"));
        var current = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType("image/jpeg"));
        var excluded = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType("image/png"));
        var encrypted = await SeedFileWithVersionAsync(
            configureVersion: v => v
                .WithMimeType("image/jpeg")
                .WithIsEncrypted(true)
                .WithEncryptionIv(new byte[12])
                .WithEncryptionSalt(new byte[16])
                .WithIntegrityTag(new byte[16]));
        var document = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType("text/plain"));

        await UpsertAsync(outdated.CurrentVersionId!.Value, latitude: null, extractorVersion: 1);
        await UpsertAsync(current.CurrentVersionId!.Value, latitude: null, extractorVersion: 2);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await db.ContentObjects.ExecuteUpdateAsync(
            setters => setters.SetProperty(c => c.IsPromoted, true),
            TestContext.Current.CancellationToken);

        var unpromoted = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType("image/jpeg"));

        var candidates = await unitOfWork.CaptureMetadata.GetExtractionCandidatesAsync(
            extractorVersion: 2,
            limit: 50,
            excludedVersionIds: [excluded.CurrentVersionId!.Value],
            TestContext.Current.CancellationToken);

        candidates.Should().BeEquivalentTo([wanted.CurrentVersionId!.Value, outdated.CurrentVersionId.Value]);
        candidates.Should().NotContain(encrypted.CurrentVersionId!.Value);
        candidates.Should().NotContain(document.CurrentVersionId!.Value);
        candidates.Should().NotContain(unpromoted.CurrentVersionId!.Value);
    }
}
