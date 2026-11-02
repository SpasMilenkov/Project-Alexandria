using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Tests.Common.Builders;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Services;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using IoDirectory = System.IO.Directory;
using IoFile = System.IO.File;

namespace Alexandria.Tests.Unit.MediaMetadata;

public class StagingServiceTests : IDisposable
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly IContentStorageService _storage = Substitute.For<IContentStorageService>();
    private readonly string _stagedDir = IoDirectory.CreateTempSubdirectory("staging-tests").FullName;
    private readonly Guid _fileId = Guid.NewGuid();

    public StagingServiceTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.FileVersions.Returns(_versions);

        var version = new FileVersionBuilder()
            .WithMimeType("audio/mpeg")
            .WithContentObjectId(Guid.NewGuid())
            .Build();
        var file = new FileBuilder()
            .WithId(_fileId)
            .WithCurrentVersionId(version.Id)
            .Build();

        _files.GetByIdAsync(_fileId, Arg.Any<CancellationToken>()).Returns(file);
        _versions.FirstOrDefaultAsync(Arg.Any<Expression<Func<FileVersion, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(version);
    }

    public void Dispose()
    {
        IoDirectory.Delete(_stagedDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private StagingService CreateSut() => new(
        _unitOfWork,
        _storage,
        Options.Create(new EssentiaConfig { StagedDir = _stagedDir }),
        Substitute.For<ILogger<StagingService>>());

    private void DownloadWrites(string content) =>
        _storage.DownloadContentObjectAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => IoFile.WriteAllTextAsync(ci.ArgAt<string>(1), content));

    [Fact]
    public async Task StageAsync_sameFileTwice_stagesSeparateCopies()
    {
        var sut = CreateSut();

        DownloadWrites("version-1");
        var first = await sut.StageAsync(_fileId, TestContext.Current.CancellationToken);

        DownloadWrites("version-2");
        var second = await sut.StageAsync(_fileId, TestContext.Current.CancellationToken);

        first!.FilePath.Should().NotBe(second!.FilePath);
        (await IoFile.ReadAllTextAsync(first.FilePath, TestContext.Current.CancellationToken))
            .Should().Be("version-1");
        (await IoFile.ReadAllTextAsync(second.FilePath, TestContext.Current.CancellationToken))
            .Should().Be("version-2");
    }

    [Fact]
    public async Task StageAsync_downloadFails_removesPartialFile()
    {
        _storage.DownloadContentObjectAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await IoFile.WriteAllTextAsync(ci.ArgAt<string>(1), "partial");
                throw new IOException("connection reset");
            });

        var act = () => CreateSut().StageAsync(_fileId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<IOException>();
        IoDirectory.EnumerateFiles(_stagedDir, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task Discard_stagedFile_removesOnlyThatCopy()
    {
        var sut = CreateSut();
        DownloadWrites("audio");

        var discarded = await sut.StageAsync(_fileId, TestContext.Current.CancellationToken);
        var kept = await sut.StageAsync(_fileId, TestContext.Current.CancellationToken);

        sut.Discard(discarded!);

        IoFile.Exists(discarded!.FilePath).Should().BeFalse();
        IoFile.Exists(kept!.FilePath).Should().BeTrue();
    }
}
