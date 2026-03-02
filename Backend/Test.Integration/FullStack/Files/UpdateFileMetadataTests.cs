using System.Net;
using System.Net.Http.Json;
using API.Features.Storage.Files.UpdateFileMetadata;
using AwesomeAssertions;
using Test.Common.Builders;
using Test.Common.Fixtures;
using Xunit;

namespace Test.Integration.FullStack.Files;

public class UpdateFileMetadataTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private static string Route(Guid id) => $"/api/files/{id}/metadata";

    [Fact]
    public async Task Rename_Returns200_WithNewName()
    {
        var file = await SeedFileWithVersionAsync();
        var req = new UpdateFileMetadataRequest { Id = file.Id, Name = "new-name.txt" };

        var response = await Auth.PatchAsJsonAsync(Route(file.Id), req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UpdateFileMetadataResponse>(cancellationToken: TestContext.Current.CancellationToken);
        body!.Name.Should().Be("new-name.txt");
    }

    [Fact]
    public async Task NoFieldsProvided_Returns400()
    {
        var file = await SeedFileWithVersionAsync();
        var req = new UpdateFileMetadataRequest { Id = file.Id };

        var response = await Auth.PatchAsJsonAsync(Route(file.Id), req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NameTooLong_Returns400()
    {
        var file = await SeedFileWithVersionAsync();
        var req = new UpdateFileMetadataRequest { Id = file.Id, Name = new string('a', 256) };

        var response = await Auth.PatchAsJsonAsync(Route(file.Id), req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NonExistentFile_Returns404()
    {
        var req = new UpdateFileMetadataRequest { Id = Guid.NewGuid(), Name = "name.txt" };

        var response = await Auth.PatchAsJsonAsync(Route(Guid.NewGuid()), req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Bug", "true")]
    // UpdateFileMetadata has AllowAnonymous() but calls User.GetUserId() internally — throws without auth
    public async Task Unauthenticated_Throws()
    {
        var file = await SeedFileWithVersionAsync();
        var req = new UpdateFileMetadataRequest { Id = file.Id, Name = "anon.txt" };

        var response = await Anon.PatchAsJsonAsync(Route(file.Id), req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Bug", "true")]
    // UpdateFileMetadata has no OwnerId filter — any authenticated user can rename any file
    public async Task AnyUserCanUpdateAnyFile()
    {
        var file = await SeedFileWithVersionAsync(); // owned by UserId
        var (otherClient, _) = await CreateOtherUserAsync();
        var req = new UpdateFileMetadataRequest { Id = file.Id, Name = "hacked.txt" };

        var response = await otherClient.PatchAsJsonAsync(Route(file.Id), req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
