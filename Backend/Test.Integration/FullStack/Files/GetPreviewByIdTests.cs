using System.Net;
using AwesomeAssertions;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Files;

public class GetPreviewByIdTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static string Route(Guid id) => $"/api/files/{id}/preview";

    [Fact]
    public async Task FileWithNoPreview_Returns200()
    {
        // When no preview is available, the service returns null and the endpoint
        // exits early with a 200 and an empty body.
        var file = await SeedFileWithVersionAsync(fb => fb.WithHasPreview(false));

        var response = await Auth.GetAsync(Route(file.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonExistentFile_Returns200()
    {
        // The preview service returns null for unknown files; the endpoint exits early
        // with a 200 and no body.
        var response = await Auth.GetAsync(Route(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        var file = await SeedFileWithVersionAsync();

        var response = await Anon.GetAsync(Route(file.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
