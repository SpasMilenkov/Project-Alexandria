using Alexandria.Data.Models;
using Alexandria.Repositories;
using Alexandria.Tests.Common.Builders;
using Alexandria.Tests.Common.Fixtures;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Directory = Alexandria.Data.Models.Directory;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Integration.Storage;

/// <summary>
/// A copy shares the source's stored bytes, so a client-encrypted source has to stay encrypted
/// with the same key material, or the copy reads as plain text over ciphertext.
/// </summary>
[Collection("Database")]
public class CopyEncryptionStateTests(DatabaseFixture fixture) : IAsyncLifetime
{
    // Lengths the check constraints require: a 12-byte AES-GCM IV, 16-byte salt and tag
    private static readonly byte[] Iv = Enumerable.Repeat((byte)1, 12).ToArray();
    private static readonly byte[] Salt = Enumerable.Repeat((byte)2, 16).ToArray();
    private static readonly byte[] Tag = Enumerable.Repeat((byte)3, 16).ToArray();

    private readonly Guid _userId = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using var db = fixture.CreateDbContext();
        db.Users.Add(new UserBuilder().WithId(_userId).Build());
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await fixture.ResetAsync();
    }

    [Fact]
    public async Task CopyFiles_encryptedSource_copyKeepsTheEncryptionState()
    {
        var source = await SeedDirectoryAsync("Source", parentId: null);
        var destination = await SeedDirectoryAsync("Destination", parentId: null);
        var file = await SeedEncryptedFileAsync(source.Id);

        await using (var db = fixture.CreateDbContext())
            await new FileRepository(db).CopyFilesAsync([file.Id], destination.Id, _userId, Ct);

        var copy = await GetCurrentVersionInAsync(destination.Id);

        AssertEncryptedLikeTheSource(copy);
    }

    [Fact]
    public async Task CopyDirectory_encryptedSourceInASubfolder_copyKeepsTheEncryptionState()
    {
        var source = await SeedDirectoryAsync("Source", parentId: null);
        var nested = await SeedDirectoryAsync("Nested", parentId: source.Id);
        var destination = await SeedDirectoryAsync("Destination", parentId: null);
        await SeedEncryptedFileAsync(nested.Id);

        await using (var db = fixture.CreateDbContext())
            await new DirectoryRepository(db).CopyDirectoryAsync(source.Id, destination.Id, _userId, Ct);

        Guid copiedNestedId;
        await using (var db = fixture.CreateDbContext())
        {
            copiedNestedId = await db.Directories
                .Where(d => d.Name == "Nested" && d.Id != nested.Id)
                .Select(d => d.Id)
                .SingleAsync(Ct);
        }

        var copy = await GetCurrentVersionInAsync(copiedNestedId);

        AssertEncryptedLikeTheSource(copy);
    }

    private static void AssertEncryptedLikeTheSource(FileVersion copy)
    {
        copy.IsEncrypted.Should().BeTrue();
        copy.EncryptionIv.Should().Equal(Iv);
        copy.EncryptionSalt.Should().Equal(Salt);
        copy.IntegrityTag.Should().Equal(Tag);
        copy.EncryptionHint.Should().Be("hint");
        copy.KdfVersion.Should().Be(1);
        copy.IterationCount.Should().Be(600_000);
    }

    private async Task<FileVersion> GetCurrentVersionInAsync(Guid directoryId)
    {
        await using var db = fixture.CreateDbContext();

        return await db.Files
            .Where(f => f.DirectoryId == directoryId)
            .Select(f => f.CurrentVersion)
            .SingleAsync(Ct);
    }

    private async Task<Directory> SeedDirectoryAsync(string name, Guid? parentId)
    {
        await using var db = fixture.CreateDbContext();
        var directory = new DirectoryBuilder().WithOwner(_userId).WithName(name).WithParent(parentId).Build();

        db.Directories.Add(directory);
        await db.SaveChangesAsync(Ct);

        return directory;
    }

    private async Task<File> SeedEncryptedFileAsync(Guid directoryId)
    {
        await using var db = fixture.CreateDbContext();

        var content = new ContentObjectBuilder().Build();
        var version = new FileVersionBuilder()
            .WithContentObject(content)
            .WithCreatedBy(_userId)
            .WithMimeType("audio/mpeg")
            .WithIsEncrypted(true)
            .WithEncryptionIv(Iv)
            .WithEncryptionSalt(Salt)
            .WithIntegrityTag(Tag)
            .WithEncryptionHint("hint")
            .WithKdfVersion(1)
            .WithIterationCount(600_000)
            .Build();
        var file = new FileBuilder()
            .WithOwner(_userId)
            .WithDirectory(directoryId)
            .WithMimeType("audio/mpeg")
            .Build();

        version.File = file;
        db.AddRange(content, version, file);
        await db.SaveChangesAsync(Ct);

        // Set after the insert because File and FileVersion reference each other
        file.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(Ct);

        return file;
    }
}
