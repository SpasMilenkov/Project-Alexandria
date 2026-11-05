using System.Net;
using System.Net.Http.Json;
using Alexandria.Dto.Metrics;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class StorageBreakdownTrashTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/storage/my-storage";

    private static DateTime Today => DateOnly.FromDateTime(DateTime.UtcNow).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    [Fact]
    public async Task GetMyStorage_trash_counts_only_files_inside_restore_window()
    {
        var ct = TestContext.Current.CancellationToken;

        await SeedFileWithVersionAsync(
            fb => fb.WithDeletedAt(Today.AddDays(-2).AddHours(12)),
            vb => vb.WithSize(700L));
        await SeedFileWithVersionAsync(
            fb => fb.WithDeletedAt(Today.AddDays(-45).AddHours(12)),
            vb => vb.WithSize(9_000L));
        await SeedFileWithVersionAsync();

        var response = await Auth.GetAsync(Route, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var breakdown = await response.Content.ReadFromJsonAsync<StorageBreakdown>(cancellationToken: ct);
        breakdown!.TrashSize.Should().Be(700L);
    }
}
