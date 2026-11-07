using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Alexandria.Api.Features.Storage.Files.FinalizeFileUpload;
using Alexandria.Api.Features.Storage.Files.FinalizeFileUploadBatch;
using Alexandria.Api.Features.Storage.Files.InitiateFileUpload;
using Alexandria.Api.Features.Storage.Files.InitiateFileUploadBatch;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Context;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Fixtures;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Blake3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class UploadBatchTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string InitRoute = "/api/files/init-upload/batch";
    private const string FinalizeRoute = "/api/files/finalize-upload/batch";
    private const string ContentType = "application/octet-stream";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IAmazonS3 S3 => Factory.Services.GetRequiredService<IAmazonS3>();
    private S3Config S3Config => Factory.Services.GetRequiredService<IOptions<S3Config>>().Value;

    private static byte[] UniqueBytes() =>
        [.. Guid.NewGuid().ToByteArray(), .. Guid.NewGuid().ToByteArray()];

    private static byte[] Blake3Digest(byte[] bytes)
    {
        using var hasher = Hasher.New();
        hasher.Update(bytes);
        return hasher.Finalize().AsSpan().ToArray();
    }

    private static InitializeFileUploadRequest InitItem(byte[] bytes) => new()
    {
        ContentType = ContentType,
        Hash = Convert.ToHexString(Blake3Digest(bytes)),
        ContentLength = bytes.Length
    };

    private async Task<List<InitializeFileUploadBatchResult>> InitAsync(params InitializeFileUploadRequest[] items)
    {
        var response = await Auth.PostAsJsonAsync(
            InitRoute,
            new InitializeFileUploadBatchRequest { Items = [.. items] },
            Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<InitializeFileUploadBatchResponse>(Ct);

        return body!.Results;
    }

    // Signed URLs are always https, but the test Garage only speaks plain http. The signature
    // covers the host rather than the scheme, so this keeps the URL valid.
    private static async Task<HttpResponseMessage> PutAsync(string uploadUrl, byte[] bytes)
    {
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(ContentType);

        using var http = new HttpClient();

        return await http.PutAsync(uploadUrl.Replace("https://", "http://"), content, Ct);
    }

    private static async Task PutOkAsync(string uploadUrl, byte[] bytes)
    {
        var response = await PutAsync(uploadUrl, bytes);

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    private static FinalizeFileUploadRequest FinalizeItem(Guid uploadId, string fileName) => new()
    {
        UploadId = uploadId,
        FileName = fileName
    };

    private async Task<List<FinalizeFileUploadBatchResult>> FinalizeAsync(
        HttpClient client,
        params FinalizeFileUploadRequest[] items)
    {
        var response = await client.PostAsJsonAsync(
            FinalizeRoute,
            new FinalizeFileUploadBatchRequest { Items = [.. items] },
            Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<FinalizeFileUploadBatchResponse>(Ct);

        return body!.Results;
    }

    private async Task<(Guid UploadId, string Url)> InitAndPutAsync(byte[] bytes)
    {
        var results = await InitAsync(InitItem(bytes));
        var result = results.Single();

        await PutOkAsync(result.UploadUrl!, bytes);

        return (result.UploadId!.Value, result.UploadUrl!);
    }

    private async Task<bool> TempKeyExistsAsync(string key)
    {
        try
        {
            await S3.GetObjectMetadataAsync(S3Config.TempBucket, key, Ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private async Task<byte[]> ReadObjectAsync(string bucket, string key)
    {
        using var response = await S3.GetObjectAsync(bucket, key, Ct);
        using var buffer = new MemoryStream();

        await response.ResponseStream.CopyToAsync(buffer, Ct);

        return buffer.ToArray();
    }

    [Fact]
    public async Task Init_invalid_item_fails_alone()
    {
        var bytes = UniqueBytes();
        var invalid = InitItem(bytes);
        invalid.Hash = "not-a-hash";

        var results = await InitAsync(InitItem(bytes), invalid, InitItem(UniqueBytes()));

        results.Select(r => r.Error is null).Should().Equal(true, false, true);
        results[1].UploadId.Should().BeNull();
        results[1].UploadUrl.Should().BeNull();
        results[0].UploadUrl.Should().Contain("/incoming/");
    }

    [Fact]
    public async Task Init_another_users_directory_fails_only_those_items()
    {
        var bytes = UniqueBytes();
        var foreign = InitItem(bytes);
        foreign.DirectoryId = Guid.NewGuid();

        var results = await InitAsync(InitItem(bytes), foreign);

        results[0].Error.Should().BeNull();
        results[1].Error.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(InitRoute)]
    [InlineData(FinalizeRoute)]
    public async Task Empty_batch_returns_400(string route)
    {
        var response = await Auth.PostAsJsonAsync(route, new { items = Array.Empty<object>() }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Init_batch_over_the_limit_returns_400()
    {
        var items = Enumerable.Range(0, 101).Select(_ => InitItem(UniqueBytes())).ToList();

        var response = await Auth.PostAsJsonAsync(
            InitRoute,
            new InitializeFileUploadBatchRequest { Items = items },
            Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Finalize_batch_over_the_limit_returns_400()
    {
        var items = Enumerable.Range(0, 101).Select(_ => FinalizeItem(Guid.NewGuid(), "file.bin")).ToList();

        var response = await Auth.PostAsJsonAsync(
            FinalizeRoute,
            new FinalizeFileUploadBatchRequest { Items = items },
            Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(InitRoute)]
    [InlineData(FinalizeRoute)]
    public async Task Anonymous_request_is_rejected(string route)
    {
        var response = await Anon.PostAsJsonAsync(route, new { items = new[] { new { } } }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Finalize_corrupted_file_fails_alone_and_its_objects_are_removed()
    {
        var good = UniqueBytes();
        var corrupted = UniqueBytes();
        var alsoGood = UniqueBytes();

        var init = await InitAsync(InitItem(good), InitItem(corrupted), InitItem(alsoGood));

        await PutOkAsync(init[0].UploadUrl!, good);
        await PutOkAsync(init[1].UploadUrl!, UniqueBytes());
        await PutOkAsync(init[2].UploadUrl!, alsoGood);

        var results = await FinalizeAsync(
            Auth,
            FinalizeItem(init[0].UploadId!.Value, "good.bin"),
            FinalizeItem(init[1].UploadId!.Value, "corrupted.bin"),
            FinalizeItem(init[2].UploadId!.Value, "also-good.bin"));

        results.Select(r => r.Success).Should().Equal(true, false, true);
        results[1].Error.Should().NotBeNullOrEmpty();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var names = await db.Files.Where(f => f.OwnerId == UserId).Select(f => f.Name).ToListAsync(Ct);
        names.Should().BeEquivalentTo("good.bin", "also-good.bin");

        var failed = await db.Uploads.AsNoTracking().SingleAsync(u => u.Id == init[1].UploadId, Ct);
        failed.Status.Should().Be(UploadStatus.Interrupted);

        (await TempKeyExistsAsync($"content/{failed.TempObjectKey}")).Should().BeFalse();
        (await TempKeyExistsAsync($"incoming/{failed.TempObjectKey}")).Should().BeFalse();
    }

    [Fact]
    public async Task Finalize_never_uploaded_file_fails_alone()
    {
        var good = UniqueBytes();
        var missing = UniqueBytes();

        var init = await InitAsync(InitItem(good), InitItem(missing));
        await PutOkAsync(init[0].UploadUrl!, good);

        var results = await FinalizeAsync(
            Auth,
            FinalizeItem(init[0].UploadId!.Value, "good.bin"),
            FinalizeItem(init[1].UploadId!.Value, "missing.bin"));

        results.Select(r => r.Success).Should().Equal(true, false);
    }

    [Fact]
    public async Task Finalize_invalid_encryption_details_fail_alone()
    {
        var good = UniqueBytes();
        var (uploadId, _) = await InitAndPutAsync(good);

        var invalid = FinalizeItem(Guid.NewGuid(), "secret.bin");
        invalid.IsEncrypted = true;
        invalid.EncryptionIv = new byte[3];

        var results = await FinalizeAsync(Auth, FinalizeItem(uploadId, "good.bin"), invalid);

        results.Select(r => r.Success).Should().Equal(true, false);
    }

    [Fact]
    public async Task Finalize_stores_each_encryption_value_in_its_own_column()
    {
        var bytes = UniqueBytes();
        var (uploadId, _) = await InitAndPutAsync(bytes);

        // Distinct contents, because a swapped IV, salt or tag would make the file undecryptable.
        var iv = Enumerable.Repeat((byte)1, 12).ToArray();
        var salt = Enumerable.Repeat((byte)2, 16).ToArray();
        var tag = Enumerable.Repeat((byte)3, 16).ToArray();

        var item = FinalizeItem(uploadId, "secret.bin");
        item.IsEncrypted = true;
        item.EncryptionIv = iv;
        item.EncryptionSalt = salt;
        item.IntegrityTag = tag;
        item.EncryptionHint = "hint";
        item.IterationCount = 800_000;

        var results = await FinalizeAsync(Auth, item);

        results[0].Success.Should().BeTrue(results[0].Error);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var version = await db.FileVersions.AsNoTracking().SingleAsync(v => v.File.Name == "secret.bin", Ct);

        version.IsEncrypted.Should().BeTrue();
        version.EncryptionIv.Should().Equal(iv);
        version.EncryptionSalt.Should().Equal(salt);
        version.IntegrityTag.Should().Equal(tag);
        version.EncryptionHint.Should().Be("hint");
        version.IterationCount.Should().Be(800_000);
    }

    [Fact]
    public async Task Finalize_identical_files_share_one_content_object()
    {
        var bytes = UniqueBytes();

        var init = await InitAsync(InitItem(bytes), InitItem(bytes), InitItem(bytes));

        foreach (var result in init)
            await PutOkAsync(result.UploadUrl!, bytes);

        var results = await FinalizeAsync(
            Auth,
            FinalizeItem(init[0].UploadId!.Value, "one.bin"),
            FinalizeItem(init[1].UploadId!.Value, "two.bin"),
            FinalizeItem(init[2].UploadId!.Value, "three.bin"));

        results.Should().OnlyContain(r => r.Success);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var hash = Blake3Digest(bytes);
        var contentObjects = await db.ContentObjects.AsNoTracking().ToListAsync(Ct);

        contentObjects.Count(c => c.Hash.SequenceEqual(hash)).Should().Be(1);
        (await db.Files.CountAsync(f => f.OwnerId == UserId, Ct)).Should().Be(3);
    }

    [Fact]
    public async Task Finalize_repeating_a_batch_succeeds_without_a_second_version()
    {
        var bytes = UniqueBytes();
        var (uploadId, _) = await InitAndPutAsync(bytes);
        var item = FinalizeItem(uploadId, "repeated.bin");

        var first = await FinalizeAsync(Auth, item);
        var second = await FinalizeAsync(Auth, item);

        first[0].Success.Should().BeTrue();
        second[0].Success.Should().BeTrue();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var versions = await db.FileVersions.CountAsync(v => v.File.Name == "repeated.bin", Ct);
        versions.Should().Be(1);
    }

    [Fact]
    public async Task Finalize_concurrent_requests_for_one_upload_create_one_version()
    {
        var bytes = UniqueBytes();
        var (uploadId, _) = await InitAndPutAsync(bytes);
        var item = FinalizeItem(uploadId, "concurrent.bin");

        var outcomes = await Task.WhenAll(FinalizeAsync(Auth, item), FinalizeAsync(Auth, item));

        outcomes.Should().Contain(results => results[0].Success);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var versions = await db.FileVersions.CountAsync(v => v.File.Name == "concurrent.bin", Ct);
        versions.Should().Be(1);

        var upload = await db.Uploads.AsNoTracking().SingleAsync(u => u.Id == uploadId, Ct);
        upload.Status.Should().Be(UploadStatus.Finished);
    }

    [Fact]
    public async Task Finalize_another_users_upload_fails_and_stays_untouched()
    {
        var bytes = UniqueBytes();
        var (uploadId, _) = await InitAndPutAsync(bytes);

        using var other = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "Admin");

        var results = await FinalizeAsync(other, FinalizeItem(uploadId, "stolen.bin"));

        results[0].Success.Should().BeFalse();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var upload = await db.Uploads.AsNoTracking().SingleAsync(u => u.Id == uploadId, Ct);
        upload.Status.Should().Be(UploadStatus.InProgress);

        (await db.Files.AnyAsync(f => f.Name == "stolen.bin", Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Writing_to_the_url_after_finalize_cannot_change_the_promoted_bytes()
    {
        var original = UniqueBytes();
        var (uploadId, url) = await InitAndPutAsync(original);

        var results = await FinalizeAsync(Auth, FinalizeItem(uploadId, "sealed.bin"));
        results[0].Success.Should().BeTrue();

        // The URL is still inside its lifetime, so Garage accepts this write.
        await PutOkAsync(url, UniqueBytes());

        var hash = Blake3Digest(original);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var upload = await db.Uploads.AsNoTracking().SingleAsync(u => u.Id == uploadId, Ct);
        var contentObject = await db.ContentObjects.AsNoTracking().SingleAsync(c => c.UploadId == uploadId, Ct);

        var sealedBytes = await ReadObjectAsync(S3Config.TempBucket, $"content/{upload.TempObjectKey}");
        sealedBytes.Should().Equal(original);

        var promotions = scope.ServiceProvider.GetRequiredService<IPromotionService>();
        var promoted = await promotions.TryPromoteContentObjectAsync(contentObject.Id, upload.TempObjectKey, Ct);
        promoted.Should().BeTrue();

        var permanentBytes = await ReadObjectAsync(
            S3Config.UploadBucket,
            $"content/{Convert.ToHexStringLower(hash)}");

        permanentBytes.Should().Equal(original);
    }
}
