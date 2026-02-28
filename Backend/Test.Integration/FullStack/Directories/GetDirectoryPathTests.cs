using System.Net;
using System.Net.Http.Json;
using API.Features.Storage.Directories.GetDirectoryPath;
using AwesomeAssertions;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Directories;

public class GetDirectoryPathTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static string Route(Guid id) => $"/api/directories/path/{id}";

    [Fact]
    public async Task RootDirectory_ReturnsSinglePathPart()
    {
        var dir = await SeedDirectoryAsync();

        var response = await Auth.GetAsync(Route(dir.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDirectoryPathResponse>();
        body!.PathParts.Should().HaveCount(1);
        body.PathParts[0].Id.Should().Be(dir.Id);
        body.PathParts[0].Name.Should().Be(dir.Name);
    }

    [Fact]
    public async Task NestedDirectory_ReturnsFullBreadcrumb()
    {
        var grandparent = await SeedDirectoryAsync();
        var parent = await SeedDirectoryAsync(parentId: grandparent.Id);
        var child = await SeedDirectoryAsync(parentId: parent.Id);

        var response = await Auth.GetAsync(Route(child.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetDirectoryPathResponse>();
        body!.PathParts.Should().HaveCount(3);
        body.PathParts[0].Id.Should().Be(grandparent.Id);
        body.PathParts[1].Id.Should().Be(parent.Id);
        body.PathParts[2].Id.Should().Be(child.Id);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var dir = await SeedDirectoryAsync();

        var response = await Anon.GetAsync(Route(dir.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
