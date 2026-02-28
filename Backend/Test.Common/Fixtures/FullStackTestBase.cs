using Data.Context;
using Microsoft.Extensions.DependencyInjection;
using Models;
using Test.Common.Builders;
using Xunit;
using Directory = Models.Directory;
using File = Models.File;

namespace Test.Common.Fixtures;

[Collection("FullStack")]
public abstract class FullStackTestBase : IAsyncLifetime
{
    protected readonly AlexandriaFixture Fixture;
    protected AlexandriaWebFactory Factory = null!;
    protected HttpClient Anon = null!;
    protected HttpClient Auth = null!;
    protected readonly Guid UserId = Guid.NewGuid();

    protected FullStackTestBase(AlexandriaFixture fixture)
    {
        Fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        Factory = new AlexandriaWebFactory(Fixture);
        Anon = Factory.CreateClient();
        Auth = Factory.CreateAuthenticatedClient(UserId);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        db.Users.Add(new UserBuilder().WithId(UserId).Build());
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Anon.Dispose();
        Auth.Dispose();
        await Factory.DisposeAsync();
        await Fixture.ResetAsync();
    }

    /// <summary>
    /// Returns an authenticated HttpClient for a user other than the primary test user.
    /// Also seeds that user in the database.
    /// </summary>
    protected async Task<(HttpClient Client, Guid OtherUserId)> CreateOtherUserAsync()
    {
        var otherId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();
        db.Users.Add(new UserBuilder().WithId(otherId).Build());
        await db.SaveChangesAsync();
        return (Factory.CreateAuthenticatedClient(otherId), otherId);
    }

    /// <summary>
    /// Seeds a File with a linked FileVersion and ContentObject, handling the
    /// circular FK (File.CurrentVersionId ↔ FileVersion.FileId) via two SaveChanges passes.
    /// The file is owned by <see cref="UserId"/> by default; pass a configure delegate to override.
    /// </summary>
    protected async Task<File> SeedFileWithVersionAsync(
        Action<FileBuilder>? configure = null,
        Action<FileVersionBuilder>? configureVersion = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var co = new ContentObjectBuilder().Build();

        var fvb = new FileVersionBuilder()
            .WithContentObject(co)
            .WithCreatedBy(UserId);
        configureVersion?.Invoke(fvb);
        var fv = fvb.Build();

        var fb = new FileBuilder().WithOwner(UserId);
        configure?.Invoke(fb);
        var file = fb.Build();

        db.ContentObjects.Add(co);
        db.FileVersions.Add(fv);
        db.Files.Add(file);
        await db.SaveChangesAsync();

        // Second pass: wire up the circular FK
        file.CurrentVersionId = fv.Id;
        fv.FileId = file.Id;
        db.Files.Update(file);
        db.FileVersions.Update(fv);
        await db.SaveChangesAsync();

        return file;
    }

    /// <summary>
    /// Seeds a Directory owned by <see cref="UserId"/>. Pass a configure delegate to override defaults.
    /// </summary>
    protected async Task<Directory> SeedDirectoryAsync(
        Guid? parentId = null,
        Action<DirectoryBuilder>? configure = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlexandriaDbContext>();

        var builder = new DirectoryBuilder().WithOwner(UserId).WithParent(parentId);
        configure?.Invoke(builder);
        var dir = builder.Build();

        db.Directories.Add(dir);
        await db.SaveChangesAsync();

        return dir;
    }
}
