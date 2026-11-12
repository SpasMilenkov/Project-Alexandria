using Alexandria.Repositories;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.Audit;

/// <summary>
/// A version download's audit row takes the file and version number from the same query that
/// authorises the download, so that projection has to carry them for the owner only.
/// </summary>
public class VersionDownloadMetadataTests(DatabaseFixture fixture) : AuditTriggerTestBase(fixture)
{
    [Fact]
    public async Task GetDownloadMetadata_carriesTheFileAndVersionNumber()
    {
        var file = await SeedFileAsync("report.pdf");

        await using var db = Fixture.CreateDbContext();
        var result = await new FileVersionRepository(db).GetDownloadMetadataAsync(file.CurrentVersionId!.Value,
            UserId, Ct);

        result.Should().NotBeNull();
        result!.Value.FileId.Should().Be(file.Id);
        result.Value.VersionNumber.Should().Be(1);
        result.Value.Metadata.FileName.Should().Be("report.pdf");
    }

    [Fact]
    public async Task GetDownloadMetadata_someoneElsesVersion_returnsNull()
    {
        var file = await SeedFileAsync("report.pdf");

        await using var db = Fixture.CreateDbContext();
        var result = await new FileVersionRepository(db).GetDownloadMetadataAsync(file.CurrentVersionId!.Value,
            Guid.NewGuid(), Ct);

        result.Should().BeNull();
    }
}
