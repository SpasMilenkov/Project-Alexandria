using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Tests.Common.Builders;
using Alexandria.Workers.MediaMetadata.Config;
using Alexandria.Workers.MediaMetadata.Services;
using Alexandria.Workers.MediaMetadata.Workers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using IoDirectory = System.IO.Directory;
using IoFile = System.IO.File;

namespace Alexandria.Tests.Unit.MediaMetadata;

public class StagingRecoveryServiceTests : IDisposable
{
    private readonly IEssentiaBatchRepository _batches = Substitute.For<IEssentiaBatchRepository>();
    private readonly string _stagedDir = IoDirectory.CreateTempSubdirectory("staging-recovery-tests").FullName;
    private readonly StagingRecoveryService _sut;

    public StagingRecoveryServiceTests()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.EssentiaBatches.Returns(_batches);
        _batches.GetDispatchedSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<EssentiaBatch>());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);
        
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

        _sut = new StagingRecoveryService(
            Substitute.For<ILogger<StagingRecoveryService>>(),
            Options.Create(new EssentiaConfig { StagedDir = _stagedDir }),
            provider);
    }

    public void Dispose()
    {
        if (IoDirectory.Exists(_stagedDir))
            IoDirectory.Delete(_stagedDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string IncomingFile()
    {
        IoDirectory.CreateDirectory(StagingPaths.IncomingDirectory(_stagedDir));
        var path = StagingPaths.NewIncomingPath(_stagedDir, Guid.NewGuid(), "audio/mpeg");
        IoFile.WriteAllText(path, "audio");
        return path;
    }

    private string BatchFolder(Guid batchId)
    {
        var dir = IoDirectory.CreateDirectory(StagingPaths.BatchDirectory(_stagedDir, batchId)).FullName;
        IoFile.WriteAllText(Path.Combine(dir, $"{Guid.NewGuid()}.mp3"), "audio");
        return dir;
    }

    private void Dispatched(params Guid[] batchIds) =>
        _batches.GetDispatchedSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(batchIds
                .Select(id => new EssentiaBatchBuilder().WithId(id).WithStatus(EssentiaBatchStatus.Dispatched).Build())
                .ToList());

    [Fact]
    public async Task removes_every_incoming_file()
    {
        var first = IncomingFile();
        var second = IncomingFile();

        await _sut.RecoverAsync(TestContext.Current.CancellationToken);

        IoFile.Exists(first).Should().BeFalse();
        IoFile.Exists(second).Should().BeFalse();
        IoDirectory.Exists(StagingPaths.IncomingDirectory(_stagedDir)).Should().BeTrue();
    }

    [Fact]
    public async Task removes_loose_files_from_the_old_layout()
    {
        var legacy = Path.Combine(_stagedDir, $"{Guid.NewGuid()}.mp3");
        await IoFile.WriteAllTextAsync(legacy, "audio", TestContext.Current.CancellationToken);

        await _sut.RecoverAsync(TestContext.Current.CancellationToken);

        IoFile.Exists(legacy).Should().BeFalse();
    }

    [Fact]
    public async Task keeps_folders_of_dispatched_batches_and_removes_the_rest()
    {
        var dispatchedId = Guid.NewGuid();
        Dispatched(dispatchedId);
        var inFlight = BatchFolder(dispatchedId);
        var orphaned = BatchFolder(Guid.NewGuid());

        await _sut.RecoverAsync(TestContext.Current.CancellationToken);

        IoDirectory.Exists(inFlight).Should().BeTrue();
        IoDirectory.Exists(orphaned).Should().BeFalse();
    }

    [Fact]
    public async Task leaves_folders_that_are_not_batches()
    {
        var other = IoDirectory.CreateDirectory(Path.Combine(_stagedDir, "not-a-batch")).FullName;

        await _sut.RecoverAsync(TestContext.Current.CancellationToken);

        IoDirectory.Exists(other).Should().BeTrue();
    }

    [Fact]
    public async Task batch_lookup_failure_keeps_batch_folders_but_still_clears_incoming()
    {
        _batches.GetDispatchedSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("db down"));
        var incoming = IncomingFile();
        var batch = BatchFolder(Guid.NewGuid());

        await _sut.RecoverAsync(TestContext.Current.CancellationToken);

        IoFile.Exists(incoming).Should().BeFalse();
        IoDirectory.Exists(batch).Should().BeTrue();
    }

    [Fact]
    public async Task skips_database_when_there_are_no_batch_folders()
    {
        IncomingFile();

        await _sut.RecoverAsync(TestContext.Current.CancellationToken);

        await _batches.DidNotReceiveWithAnyArgs().GetDispatchedSinceAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task missing_staged_directory_is_a_noop()
    {
        IoDirectory.Delete(_stagedDir, recursive: true);

        var act = () => _sut.RecoverAsync(TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }
}
