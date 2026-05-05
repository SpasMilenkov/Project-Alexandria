using System.Net;
using System.Net.Http.Json;
using Alexandria.Api.Features.Storage.Files.RestoreFiles;
using Alexandria.Data.Context;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class RestoreFilesTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/files/restore";

    [Fact]
    public async Task RestoreDeletedFile_Returns1_AndClearsDeletedAt()
    {
        var file = await SeedFileWithVersionAsync(fb => fb.WithDeletedAt(DateTime.UtcNow));
        var req = new RestoreFilesRequest { FileIds = [file.Id] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var count = await response.Content.ReadFromJsonAsync<int>(
            cancellationToken: TestContext.Current.CancellationToken);
        count.Should().Be(1);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var restoredFile = await db.Files.FindAsync(new object?[] { file.Id }, TestContext.Current.CancellationToken);
        restoredFile!.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task RestoreAlreadyActiveFile_Returns0()
    {
        var file = await SeedFileWithVersionAsync(); // not deleted
        var req = new RestoreFilesRequest { FileIds = [file.Id] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var count = await response.Content.ReadFromJsonAsync<int>(
            cancellationToken: TestContext.Current.CancellationToken);
        count.Should().Be(0);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var req = new RestoreFilesRequest { FileIds = [Guid.NewGuid()] };

        var response = await Anon.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyFileIds_Returns400()
    {
        var req = new RestoreFilesRequest { FileIds = [] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DuplicateFileIds_Returns400()
    {
        var id = Guid.NewGuid();
        var req = new RestoreFilesRequest { FileIds = [id, id] };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}