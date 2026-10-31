using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Alexandria.Api.Features.Storage.Files.FinalizeFileUpload;
using Alexandria.Api.Features.Storage.Files.InitiateFileUpload;
using Alexandria.Data.Context;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class FinalizeFileUploadTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string InitRoute = "/api/files/init-upload";
    private const string FinalizeRoute = "/api/files/finalize-upload";

    [Fact]
    public async Task HappyPath_AuthenticatedUser_CreatesFileInDb()
    {
        // First initiate an upload to get a valid UploadId
        var initReq = new InitializeFileUploadRequest
        {
            ContentType = "text/plain",
            Hash = Convert.ToHexString(new byte[32]),
            ContentLength = 13L
        };
        var initResponse =
            await Auth.PostAsJsonAsync(InitRoute, initReq, cancellationToken: TestContext.Current.CancellationToken);
        initResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initBody =
            await initResponse.Content.ReadFromJsonAsync<InitializeFileUploadResponse>(
                cancellationToken: TestContext.Current.CancellationToken);
        initBody.Should().NotBeNull();

        // PUT bytes to the S3 upload URL
        var bytes = "hello, world!"u8.ToArray();
        using var putContent = new ByteArrayContent(bytes);
        var putResponse =
            await new HttpClient().PutAsync(initBody.UploadUrl, putContent, TestContext.Current.CancellationToken);
        putResponse.IsSuccessStatusCode.Should().BeTrue();

        // Now finalize
        var finalizeReq = new FinalizeFileUploadRequest
        {
            UploadId = initBody.UploadId,
            FileName = "hello.txt"
        };
        var response = await Auth.PostAsJsonAsync(FinalizeRoute, finalizeReq,
            cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify a file was created in the DB for this user
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var fileExists = await db.Files
            .AnyAsync(f => f.OwnerId == UserId && f.Name == "hello.txt",
                cancellationToken: TestContext.Current.CancellationToken);
        fileExists.Should().BeTrue();
    }

    [Fact]
    [Trait("Bug", "true")]
    // FinalizeFileUpload has AllowAnonymous() but calls User.GetUserId() internally,
    // which throws if no authenticated user is present.
    public async Task Unauthenticated_Throws()
    {
        var req = new FinalizeFileUploadRequest
        {
            UploadId = Guid.NewGuid(),
            FileName = "test.txt"
        };

        var response =
            await Anon.PostAsJsonAsync(FinalizeRoute, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownUploadId_Returns4xx()
    {
        var req = new FinalizeFileUploadRequest
        {
            UploadId = Guid.NewGuid(),
            FileName = "ghost.txt"
        };

        var response =
            await Auth.PostAsJsonAsync(FinalizeRoute, req, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task I01_ContentBytes_OverrideDeclaredTypeAndExtension()
    {
        var outcome = await UploadAndFinalizeAsync(PngHeader, "notes.txt", "text/plain");

        var stored = await LoadStoredMimeAsync("notes.txt", outcome.UploadId);

        stored.FileMime.Should().Be("image/png");
        stored.VersionMime.Should().Be("image/png");
    }

    [Fact]
    public async Task I02_UploadAuditRow_KeepsTheDeclaredType()
    {
        var outcome = await UploadAndFinalizeAsync(PngHeader, "report.pdf", "application/pdf");

        var stored = await LoadStoredMimeAsync("report.pdf", outcome.UploadId);

        stored.FileMime.Should().Be("image/png");
        stored.UploadMime.Should().Be("application/pdf");
    }

    [Fact]
    public async Task I03_DocxExtension_ResolvesTheZipContainer()
    {
        var bytes = CreateZip(
            "[Content_Types].xml",
            "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\" />");

        var outcome = await UploadAndFinalizeAsync(bytes, "report.docx", "application/octet-stream");

        var stored = await LoadStoredMimeAsync("report.docx", outcome.UploadId);

        stored.FileMime.Should().Be(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        stored.VersionMime.Should().Be(stored.FileMime);
    }

    [Fact]
    public async Task I04_EncryptedPayload_IsNotSniffed()
    {
        var outcome = await UploadAndFinalizeAsync(
            PdfHeader,
            "secret.bin",
            "application/octet-stream",
            isEncrypted: true);

        var stored = await LoadStoredMimeAsync("secret.bin", outcome.UploadId);

        stored.FileMime.Should().Be("application/octet-stream");
        stored.VersionMime.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task I05_UnknownContent_FallsBackToTheDeclaredType()
    {
        var outcome = await UploadAndFinalizeAsync(UnknownBytes, "blob.xyz", "application/x-custom");

        var stored = await LoadStoredMimeAsync("blob.xyz", outcome.UploadId);

        stored.FileMime.Should().Be("application/x-custom");
        stored.VersionMime.Should().Be("application/x-custom");
    }

    [Fact]
    public async Task I06_UnknownContentWithoutUsableDeclaredType_FallsBackToOctetStream()
    {
        var outcome = await UploadAndFinalizeAsync(UnknownBytes, "blob.xyz", "application/octet-stream");

        var stored = await LoadStoredMimeAsync("blob.xyz", outcome.UploadId);

        stored.FileMime.Should().Be("application/octet-stream");
        stored.VersionMime.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task I07_NewVersion_ReplacesTheFileTypeAndKeepsVersionTypes()
    {
        await UploadAndFinalizeAsync(PngHeader, "same.bin", "application/octet-stream");
        var second = await UploadAndFinalizeAsync(PdfHeader, "same.bin", "application/octet-stream");

        var stored = await LoadStoredMimeAsync("same.bin", second.UploadId);

        stored.FileMime.Should().Be("application/pdf");
        stored.VersionMime.Should().Be("application/pdf");

        var versionTypes = await LoadVersionMimeTypesAsync("same.bin");

        versionTypes.Should().Equal("image/png", "application/pdf");
    }

    [Fact]
    public async Task I08_SevenZipSignature_DoesNotBecomeAZipType()
    {
        var outcome = await UploadAndFinalizeAsync(
            [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C],
            "archive.bin",
            "application/octet-stream");

        var stored = await LoadStoredMimeAsync("archive.bin", outcome.UploadId);

        stored.FileMime.Should().Be("application/x-7z-compressed");
        stored.VersionMime.Should().Be("application/x-7z-compressed");
    }

    private static readonly byte[] PngHeader =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private static readonly byte[] PdfHeader = "%PDF-1.7\n"u8.ToArray();

    private static readonly byte[] UnknownBytes = [0x00, 0x01, 0x02, 0x03, 0xFF];

    private sealed record UploadOutcome(Guid UploadId);

    private sealed record StoredMime(string FileMime, string VersionMime, string UploadMime);

    private async Task<UploadOutcome> UploadAndFinalizeAsync(
        byte[] bytes,
        string fileName,
        string declaredContentType,
        bool isEncrypted = false)
    {
        var ct = TestContext.Current.CancellationToken;

        var initRequest = new InitializeFileUploadRequest
        {
            ContentType = declaredContentType,
            Hash = Convert.ToHexString(SHA256.HashData(bytes)),
            ContentLength = bytes.Length
        };

        var initResponse = await Auth.PostAsJsonAsync(InitRoute, initRequest, cancellationToken: ct);
        initResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initBody = await initResponse.Content.ReadFromJsonAsync<InitializeFileUploadResponse>(
            cancellationToken: ct);
        initBody.Should().NotBeNull();

        using var putContent = new ByteArrayContent(bytes);
        putContent.Headers.ContentType = new MediaTypeHeaderValue(declaredContentType);

        using var plainClient = new HttpClient();
        var putResponse = await plainClient.PutAsync(initBody!.UploadUrl, putContent, ct);
        putResponse.IsSuccessStatusCode.Should().BeTrue();

        var finalizeRequest = new FinalizeFileUploadRequest
        {
            UploadId = initBody.UploadId,
            FileName = fileName,
            IsEncrypted = isEncrypted,
            EncryptionIv = isEncrypted ? new byte[12] : null,
            EncryptionSalt = isEncrypted ? new byte[16] : null,
            IntegrityTag = isEncrypted ? new byte[16] : null,
            EncryptionHint = isEncrypted ? "test" : null,
            IterationCount = isEncrypted ? 600_000 : null
        };

        var finalizeResponse = await Auth.PostAsJsonAsync(FinalizeRoute, finalizeRequest, cancellationToken: ct);
        var errorBody = await finalizeResponse.Content.ReadAsStringAsync(ct);
        finalizeResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: errorBody);

        return new UploadOutcome(initBody.UploadId);
    }

    private async Task<StoredMime> LoadStoredMimeAsync(string fileName, Guid uploadId)
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var file = await db.Files.AsNoTracking()
            .SingleAsync(f => f.OwnerId == UserId && f.Name == fileName, ct);
        var version = await db.FileVersions.AsNoTracking()
            .SingleAsync(v => v.Id == file.CurrentVersionId!.Value, ct);
        var upload = await db.Uploads.AsNoTracking()
            .SingleAsync(u => u.Id == uploadId, ct);

        return new StoredMime(file.MimeType, version.MimeType, upload.MimeType);
    }

    private async Task<List<string>> LoadVersionMimeTypesAsync(string fileName)
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var file = await db.Files.AsNoTracking()
            .SingleAsync(f => f.OwnerId == UserId && f.Name == fileName, ct);

        return await db.FileVersions.AsNoTracking()
            .Where(v => v.FileId == file.Id)
            .OrderBy(v => v.VersionNumber)
            .Select(v => v.MimeType)
            .ToListAsync(ct);
    }

    private static byte[] CreateZip(string entryName, string content)
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        return stream.ToArray();
    }
}
