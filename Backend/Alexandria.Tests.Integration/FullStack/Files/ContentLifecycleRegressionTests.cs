using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Alexandria.Api.Features.Storage.Files.DeleteFile;
using Alexandria.Api.Features.Storage.Files.FinalizeFileUpload;
using Alexandria.Api.Features.Storage.Files.InitiateFileUpload;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Services;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Cleanup;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Alexandria.Tests.Integration.FullStack.Files;

public class ContentLifecycleRegressionTests(AlexandriaFixture fixture) : FullStackTestBase(fixture)
{
    private const string InitRoute = "/api/files/init-upload";
    private const string FinalizeRoute = "/api/files/finalize-upload";
    private const string DeleteRoute = "/api/files/";

    private static byte[] UniqueBytes()
    {
        var first = Guid.NewGuid().ToByteArray();
        var second = Guid.NewGuid().ToByteArray();
        var bytes = new byte[32];
        Buffer.BlockCopy(first, 0, bytes, 0, 16);
        Buffer.BlockCopy(second, 0, bytes, 16, 16);
        return bytes;
    }

    private static string PermanentKey(byte[] hash) =>
        $"content/{Convert.ToHexStringLower(hash)}";

    private IAmazonS3 S3 => Factory.Services.GetRequiredService<IAmazonS3>();
    private S3Config S3Config => Factory.Services.GetRequiredService<IOptions<S3Config>>().Value;

    private async Task<(Guid UploadId, string TempKey)> UploadBytesAsync(
        byte[] bytes, string fileName, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        var initResponse = await Auth.PostAsJsonAsync(InitRoute, new InitializeFileUploadRequest
        {
            ContentType = "application/octet-stream",
            Hash = hash,
            ContentLength = bytes.Length
        }, ct);
        initResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var initBody = await initResponse.Content
            .ReadFromJsonAsync<InitializeFileUploadResponse>(ct);
        initBody.Should().NotBeNull();

        using var putContent = new ByteArrayContent(bytes);
        using var http = new HttpClient();
        var putResponse = await http.PutAsync(initBody!.UploadUrl, putContent, ct);
        putResponse.IsSuccessStatusCode.Should().BeTrue();

        var finalizeResponse = await Auth.PostAsJsonAsync(FinalizeRoute, new FinalizeFileUploadRequest
        {
            UploadId = initBody.UploadId,
            FileName = fileName
        }, ct);
        finalizeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var upload = await db.Uploads
            .AsNoTracking()
            .FirstAsync(u => u.Id == initBody.UploadId, ct);

        return (upload.Id, upload.TempObjectKey);
    }

    private async Task<Guid> PromoteAsync(Guid contentObjectId, string tempKey, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var promotions = scope.ServiceProvider.GetRequiredService<IPromotionService>();
        var promoted = await promotions.TryPromoteContentObjectAsync(contentObjectId, tempKey, ct);
        promoted.Should().BeTrue();
        return contentObjectId;
    }

    private async Task<ContentObject> GetContentObjectByHashAsync(byte[] hash, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var matches = await db.ContentObjects.AsNoTracking().ToListAsync(ct);
        return matches.Single(c => c.Hash.SequenceEqual(hash));
    }

    private async Task<List<ContentObject>> GetContentObjectsByHashAsync(byte[] hash, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var matches = await db.ContentObjects.AsNoTracking().ToListAsync(ct);
        return matches.Where(c => c.Hash.SequenceEqual(hash)).ToList();
    }

    private async Task<bool> KeyExistsAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            await S3.GetObjectMetadataAsync(bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private Task<bool> TempExistsAsync(string tempKey, CancellationToken ct) =>
        KeyExistsAsync(S3Config.TempBucket, $"content/{tempKey}", ct);

    private Task<bool> PermanentExistsAsync(byte[] hash, CancellationToken ct) =>
        KeyExistsAsync(S3Config.UploadBucket, PermanentKey(hash), ct);

    private async Task<byte[]> ReadPermanentBytesAsync(byte[] hash, CancellationToken ct)
    {
        using var response = await S3.GetObjectAsync(S3Config.UploadBucket, PermanentKey(hash), ct);
        using var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, ct);
        return memory.ToArray();
    }

    private async Task OrphanContentAsync(Guid fileId, CancellationToken ct)
    {
        var delete = new HttpRequestMessage(HttpMethod.Delete, DeleteRoute)
        {
            Content = JsonContent.Create(new DeleteFileRequest { Ids = [fileId] })
        };
        var deleteResponse = await Auth.SendAsync(delete, ct);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.ContentObjects.MarkOrphaned(DateTime.UtcNow, ct);
    }

    private async Task<Guid> GetFileIdByNameAsync(string fileName, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        return await db.Files
            .Where(f => f.OwnerId == UserId && f.Name == fileName)
            .Select(f => f.Id)
            .SingleAsync(ct);
    }

    [Fact]
    public async Task revive_promoted_reuses_row_and_keeps_bytes_alive()
    {
        var ct = TestContext.Current.CancellationToken;
        var bytes = UniqueBytes();
        var hash = SHA256.HashData(bytes);

        var (uploadA, tempA) = await UploadBytesAsync(bytes, "revive-a.bin", ct);
        var fileA = await GetFileIdByNameAsync("revive-a.bin", ct);
        var original = await GetContentObjectByHashAsync(hash, ct);
        await PromoteAsync(original.Id, tempA, ct);
        (await PermanentExistsAsync(hash, ct)).Should().BeTrue();

        await OrphanContentAsync(fileA, ct);
        (await GetContentObjectByHashAsync(hash, ct)).OrphanedAt.Should().NotBeNull();

        var (uploadB, tempB) = await UploadBytesAsync(bytes, "revive-b.bin", ct);

        // Exactly one row: the orphan was revived, not aliased.
        var survivors = await GetContentObjectsByHashAsync(hash, ct);
        survivors.Should().ContainSingle();

        var revived = survivors.Single();
        revived.Id.Should().Be(original.Id);
        revived.OrphanedAt.Should().BeNull();
        revived.UploadId.Should().Be(uploadB);

        // The redundant second temp copy is cleaned; the durable copy survives.
        (await TempExistsAsync(tempB, ct)).Should().BeFalse();
        (await ReadPermanentBytesAsync(hash, ct)).Should().Equal(bytes);
    }

    [Fact]
    public async Task revive_unpromoted_resets_attempts_and_stays_promotable()
    {
        var ct = TestContext.Current.CancellationToken;
        var bytes = UniqueBytes();
        var hash = SHA256.HashData(bytes);

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
            db.ContentObjects.Add(new ContentObjectBuilder()
                .WithHash(hash)
                .WithOrphanedAt(DateTime.UtcNow.AddDays(-31))
                .Build());
            await db.SaveChangesAsync(ct);
        }

        var (uploadId, tempKey) = await UploadBytesAsync(bytes, "revive-fresh.bin", ct);

        var revived = await GetContentObjectByHashAsync(hash, ct);
        revived.OrphanedAt.Should().BeNull();
        revived.UploadId.Should().Be(uploadId);

        // The scanner ignores rows at the attempt budget. The reset must have
        // happened even if the queue worker already promoted past this point.
        revived.PromotionAttempts.Should().BeLessThanOrEqualTo(1);

        await PromoteAsync(revived.Id, tempKey, ct);
        (await ReadPermanentBytesAsync(hash, ct)).Should().Equal(bytes);
    }

    [Fact]
    public async Task orphan_cleanup_with_live_version_keeps_row_and_bytes()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = await SeedFileWithVersionAsync();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var version = await db.FileVersions
            .AsNoTracking()
            .FirstAsync(v => v.FileId == file.Id, ct);
        var contentObject = await db.ContentObjects.FindAsync([version.ContentObjectId], ct);

        // Inconsistent state that MarkOrphaned alone never produces: a live
        // version on a stale orphan marker. The delete path must still refuse.
        contentObject!.OrphanedAt = DateTime.UtcNow.AddDays(-31);
        db.ContentObjects.Update(contentObject);
        await db.SaveChangesAsync(ct);

        var worker = new OrphanedCleanupWorker(
            Factory.Services,
            S3,
            Factory.Services.GetRequiredService<IOptions<S3Config>>(),
            Factory.Services.GetRequiredService<ILogger<OrphanedCleanupWorker>>());

        await worker.CleanupOrphanedContentAsync(ct);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var survivors = await verifyDb.ContentObjects
            .Where(c => c.Id == contentObject.Id)
            .ToListAsync(ct);
        survivors.Should().ContainSingle();
        survivors.Single().OrphanedAt.Should().BeNull();
    }

    [Fact]
    public async Task temp_sweeper_deletes_only_redundant_and_abandoned_temps()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;

        var abandonedKey = Guid.NewGuid().ToString();
        var redundantKey = Guid.NewGuid().ToString();
        var soleCopyKey = Guid.NewGuid().ToString();
        var exhaustedKey = Guid.NewGuid().ToString();
        var freshKey = Guid.NewGuid().ToString();
        var unknownKey = Guid.NewGuid().ToString();

        var redundantHash = SHA256.HashData(UniqueBytes());
        var soleHash = SHA256.HashData(UniqueBytes());
        var exhaustedHash = SHA256.HashData(UniqueBytes());
        var freshHash = SHA256.HashData(UniqueBytes());

        var abandoned = new UploadBuilder()
            .WithUser(UserId)
            .WithStatus(UploadStatus.InProgress)
            .WithTempObjectKey(abandonedKey)
            .Build();
        abandoned.CreatedAt = now - TimeSpan.FromHours(73);

        var redundantUpload = new UploadBuilder()
            .WithUser(UserId)
            .WithStatus(UploadStatus.Finished)
            .WithHash(redundantHash)
            .WithTempObjectKey(redundantKey)
            .WithFinishedAt(now - TimeSpan.FromHours(2))
            .Build();
        var redundantCo = new ContentObjectBuilder()
            .WithHash(redundantHash)
            .WithIsPromoted(true)
            .WithPromotedAt(now - TimeSpan.FromHours(2))
            .Build();

        var soleUpload = new UploadBuilder()
            .WithUser(UserId)
            .WithStatus(UploadStatus.Finished)
            .WithHash(soleHash)
            .WithTempObjectKey(soleCopyKey)
            .WithFinishedAt(now - TimeSpan.FromHours(2))
            .Build();
        var soleCo = new ContentObjectBuilder()
            .WithHash(soleHash)
            .Build();

        var exhaustedUpload = new UploadBuilder()
            .WithUser(UserId)
            .WithStatus(UploadStatus.Finished)
            .WithHash(exhaustedHash)
            .WithTempObjectKey(exhaustedKey)
            .WithFinishedAt(now - TimeSpan.FromHours(2))
            .Build();
        var exhaustedCo = new ContentObjectBuilder()
            .WithHash(exhaustedHash)
            .Build();
        exhaustedCo.PromotionAttempts = 10;

        var freshUpload = new UploadBuilder()
            .WithUser(UserId)
            .WithStatus(UploadStatus.Finished)
            .WithHash(freshHash)
            .WithTempObjectKey(freshKey)
            .WithFinishedAt(now - TimeSpan.FromMinutes(10))
            .Build();
        var freshCo = new ContentObjectBuilder()
            .WithHash(freshHash)
            .WithIsPromoted(true)
            .WithPromotedAt(now - TimeSpan.FromMinutes(10))
            .Build();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
            db.Uploads.AddRange(abandoned, redundantUpload, soleUpload, exhaustedUpload, freshUpload);
            db.ContentObjects.AddRange(redundantCo, soleCo, exhaustedCo, freshCo);
            await db.SaveChangesAsync(ct);
        }

        foreach (var key in new[] { abandonedKey, redundantKey, soleCopyKey, exhaustedKey, freshKey, unknownKey })
        {
            await S3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = S3Config.TempBucket,
                Key = $"content/{key}",
                InputStream = new MemoryStream(UniqueBytes()),
                ContentType = "application/octet-stream"
            }, ct);
        }

        var worker = new TempCleanupWorker(
            Factory.Services,
            S3,
            Factory.Services.GetRequiredService<IOptions<S3Config>>(),
            Factory.Services.GetRequiredService<ILogger<TempCleanupWorker>>());

        await worker.SweepTempBucketAsync(ct);

        (await TempExistsAsync(abandonedKey, ct)).Should().BeFalse();
        (await TempExistsAsync(redundantKey, ct)).Should().BeFalse();
        (await TempExistsAsync(soleCopyKey, ct)).Should().BeTrue();
        (await TempExistsAsync(exhaustedKey, ct)).Should().BeTrue();
        (await TempExistsAsync(freshKey, ct)).Should().BeTrue();
        (await TempExistsAsync(unknownKey, ct)).Should().BeTrue();

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        var abandonedRow = await verifyDb.Uploads
            .AsNoTracking()
            .SingleAsync(u => u.TempObjectKey == abandonedKey, ct);
        abandonedRow.Status.Should().Be(UploadStatus.Interrupted);
    }

    [Fact]
    public async Task migration_merges_duplicate_hashes_onto_promoted_survivor()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = $"scratch_{Guid.NewGuid():N}";

        var serverBuilder = new NpgsqlConnectionStringBuilder(Fixture.PostgresConnectionString);
        var scratchBuilder = new NpgsqlConnectionStringBuilder(Fixture.PostgresConnectionString)
        {
            Database = database
        };

        await using (var server = new NpgsqlConnection(serverBuilder.ToString()))
        {
            await server.OpenAsync(ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", server);
            await create.ExecuteNonQueryAsync(ct);
        }

        try
        {
            var options = new DbContextOptionsBuilder<AlexandriaDbContext>()
                .UseNpgsql(scratchBuilder.ToString())
                .Options;

            await using (var context = new AlexandriaDbContext(options))
            {
                await context.Database.MigrateAsync("20261011140620_AddHomeWidgetAuditColumns", ct);
            }

            var hash = SHA256.HashData(UniqueBytes());
            Guid survivorId;
            Guid firstVersionId;
            Guid secondVersionId;

            await using (var context = new AlexandriaDbContext(options))
            {
                var user = new UserBuilder().Build();
                context.Users.Add(user);

                var survivor = new ContentObjectBuilder()
                    .WithHash(hash)
                    .WithIsPromoted(true)
                    .WithPromotedAt(DateTime.UtcNow)
                    .Build();
                survivorId = survivor.Id;

                var orphan = new ContentObjectBuilder()
                    .WithHash(hash)
                    .WithIsPromoted(false)
                    .WithOrphanedAt(DateTime.UtcNow.AddDays(-31))
                    .Build();

                context.ContentObjects.AddRange(survivor, orphan);

                var firstFile = new FileBuilder().WithOwner(user.Id).WithName("m-a.bin").Build();
                var firstVersion = new FileVersionBuilder()
                    .WithContentObject(survivor)
                    .WithContentHash(hash)
                    .WithCreatedBy(user.Id)
                    .Build();
                firstVersion.File = firstFile;
                firstVersionId = firstVersion.Id;

                var secondFile = new FileBuilder().WithOwner(user.Id).WithName("m-b.bin").Build();
                var secondVersion = new FileVersionBuilder()
                    .WithContentObject(orphan)
                    .WithContentHash(hash)
                    .WithCreatedBy(user.Id)
                    .Build();
                secondVersion.File = secondFile;
                secondVersionId = secondVersion.Id;

                context.FileVersions.AddRange(firstVersion, secondVersion);
                context.Files.AddRange(firstFile, secondFile);
                await context.SaveChangesAsync(ct);

                firstFile.CurrentVersionId = firstVersion.Id;
                secondFile.CurrentVersionId = secondVersion.Id;
                context.Files.UpdateRange(firstFile, secondFile);
                await context.SaveChangesAsync(ct);
            }

            await using (var context = new AlexandriaDbContext(options))
            {
                await context.Database.MigrateAsync(ct);
            }

            await using (var context = new AlexandriaDbContext(options))
            {
                var rows = await context.ContentObjects.AsNoTracking().ToListAsync(ct);
                var survivors = rows.Where(c => c.Hash.SequenceEqual(hash)).ToList();
                survivors.Should().ContainSingle();
                survivors.Single().Id.Should().Be(survivorId);

                var versions = await context.FileVersions.AsNoTracking().ToListAsync(ct);
                versions.Single(v => v.Id == firstVersionId).ContentObjectId.Should().Be(survivorId);
                versions.Single(v => v.Id == secondVersionId).ContentObjectId.Should().Be(survivorId);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var server = new NpgsqlConnection(serverBuilder.ToString());
            await server.OpenAsync(ct);
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\"", server);
            await drop.ExecuteNonQueryAsync(ct);
        }
    }
}