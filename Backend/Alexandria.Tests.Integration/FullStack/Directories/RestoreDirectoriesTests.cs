using System.Net;
using System.Net.Http.Json;
using Alexandria.Api.Features.Storage.Directories.RestoreDirectories;
using Alexandria.Data.Context;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Directories;

public class RestoreDirectoriesTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/directories/restore";

    [Fact]
    public async Task RestoreDeletedDirectory_Returns1_AndClearsDeletedAt()
    {
        var dir = await SeedDirectoryAsync(configure: b => b.WithDeletedAt(DateTime.UtcNow));
        var req = new RestoreDirectoriesRequest { DirectoryIds = [dir.Id] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var count = await response.Content.ReadFromJsonAsync<int>(
            cancellationToken: TestContext.Current.CancellationToken);
        count.Should().Be(1);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var restored = await db.Directories.FindAsync(new object?[] { dir.Id }, TestContext.Current.CancellationToken);
        restored!.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task RestoreActiveDirectory_Returns0()
    {
        var dir = await SeedDirectoryAsync(); // not deleted
        var req = new RestoreDirectoriesRequest { DirectoryIds = [dir.Id] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var count = await response.Content.ReadFromJsonAsync<int>(
            cancellationToken: TestContext.Current.CancellationToken);
        count.Should().Be(0);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var req = new RestoreDirectoriesRequest { DirectoryIds = [Guid.NewGuid()] };

        var response = await Anon.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyArray_Returns400()
    {
        var req = new RestoreDirectoriesRequest { DirectoryIds = [] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DuplicateIds_Returns400()
    {
        var id = Guid.NewGuid();
        var req = new RestoreDirectoriesRequest { DirectoryIds = [id, id] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}