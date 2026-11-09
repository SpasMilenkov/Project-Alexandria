using System.Net;
using System.Net.Http.Json;
using Alexandria.Common;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class PasswordProtectedFlagTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    private async Task UpsertAsync(Guid versionId, bool isPasswordProtected)
    {
        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.DocumentMetadata.UpsertAsync(new DocumentMetadata
        {
            Id = Guid.NewGuid(),
            FileVersionId = versionId,
            IsPasswordProtected = isPasswordProtected,
            RawJson = "{}",
            ExtractorVersion = 1
        }, TestContext.Current.CancellationToken);
    }

    private async Task<T> GetAsync<T>(string route)
    {
        var response = await Auth.GetAsync(route, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<T>(
            cancellationToken: TestContext.Current.CancellationToken);

        return body!;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FileResult_CarriesTheStoredFlag(bool isPasswordProtected)
    {
        var file = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pptx));
        await UpsertAsync(file.CurrentVersionId!.Value, isPasswordProtected);

        var body = await GetAsync<FileResult>($"/api/files/{file.Id}");

        body.CurrentVersion.IsPasswordProtected.Should().Be(isPasswordProtected);
    }

    [Fact]
    public async Task FileResult_WithoutMetadataRow_IsNotProtected()
    {
        var file = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pptx));

        var body = await GetAsync<FileResult>($"/api/files/{file.Id}");

        body.CurrentVersion.IsPasswordProtected.Should().BeFalse();
    }

    [Fact]
    public async Task VersionHistory_CarriesTheStoredFlag()
    {
        var file = await SeedFileWithVersionAsync(configureVersion: v => v.WithMimeType(Pptx));
        await UpsertAsync(file.CurrentVersionId!.Value, isPasswordProtected: true);

        var body = await GetAsync<PaginatedResult<FileVersionDto>>(
            $"/api/files/versions?fileId={file.Id}&page=1&pageSize=10");

        body.Items.Should().ContainSingle().Which.IsPasswordProtected.Should().BeTrue();
    }
}
