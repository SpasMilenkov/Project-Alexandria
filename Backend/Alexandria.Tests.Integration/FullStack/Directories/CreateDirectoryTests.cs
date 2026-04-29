using System.Net;
using System.Net.Http.Json;
using Alexandria.Api.Features.Storage.Directories.CreateDir;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Directories;

public class CreateDirectoryTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Route = "/api/directories";

    [Fact]
    public async Task CreateRootDirectory_Returns200()
    {
        var req = new CreateDirRequest { Name = "docs", ParentId = null };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateDirResult>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Directory.Name.Should().Be("docs");
        body.Directory.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubdirectory_Returns200_WithParentId()
    {
        var parent = await SeedDirectoryAsync();
        var req = new CreateDirRequest { Name = "sub", ParentId = parent.Id };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateDirResult>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Directory.ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var req = new CreateDirRequest { Name = "secret", ParentId = null };

        var response = await Anon.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmptyName_Returns400()
    {
        var req = new CreateDirRequest { Name = "", ParentId = null };

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DuplicateNameInSameParent_Returns4xx()
    {
        var req = new CreateDirRequest { Name = "shared-name", ParentId = null };
        await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        var response = await Auth.PostAsJsonAsync(Route, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}