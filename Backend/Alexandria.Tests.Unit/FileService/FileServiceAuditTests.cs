using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Audit;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Alexandria.Tests.Unit.FileService;

public class FileServiceAuditTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly IAuditWriter _auditWriter = Substitute.For<IAuditWriter>();
    private readonly Guid _userId = Guid.NewGuid();

    public FileServiceAuditTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.FileVersions.Returns(_versions);
    }

    private Services.Storage.FileService CreateSut() => new(
        _unitOfWork,
        Substitute.For<IDirectoryService>(),
        Substitute.For<IPublisherService>(),
        _auditWriter,
        Substitute.For<ILogger<Services.Storage.FileService>>());

    [Fact]
    public async Task MoveFilesAsync_severalFiles_recordsSummaryInsideTransaction()
    {
        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid()];

        await CreateSut().MoveFilesAsync(ids, null, _userId, TestContext.Current.CancellationToken);

        _auditWriter.Received(1).Record(Arg.Is<AuditEntry>(e =>
            e.EventCode == AuditEventCode.FilesMovedBulk && e.OwnerId == _userId));

        Received.InOrder(() =>
        {
            _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>());
            _files.MoveFilesAsync(ids, null, _userId, Arg.Any<CancellationToken>());
            _auditWriter.Record(Arg.Any<AuditEntry>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task MoveFilesAsync_singleFile_leavesItToTheTrigger()
    {
        await CreateSut().MoveFilesAsync([Guid.NewGuid()], null, _userId, TestContext.Current.CancellationToken);

        _auditWriter.DidNotReceive().Record(Arg.Any<AuditEntry>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveFilesAsync_repositoryFails_rollsBack()
    {
        _files.MoveFilesAsync(Arg.Any<Guid[]>(), Arg.Any<Guid?>(), _userId, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("not owned"));

        var act = () => CreateSut().MoveFilesAsync([Guid.NewGuid()], null, _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFilesAsync_severalFiles_recordsBulkSummary()
    {
        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        _files.MarkAsDeletedAsync(ids, _userId, Arg.Any<CancellationToken>()).Returns(3);
        _versions.SoftDeleteFileVersionsAsync(ids, _userId, Arg.Any<CancellationToken>()).Returns(4);

        await CreateSut().DeleteFilesAsync(ids, _userId, hardDelete: false, TestContext.Current.CancellationToken);

        _auditWriter.Received(1).Record(Arg.Is<AuditEntry>(e =>
            e.EventCode == AuditEventCode.FilesDeletedBulk
            && e.OperationType == OperationType.Delete
            && (int)e.Metadata!["file_count"]! == 3
            && (int)e.Metadata!["version_count"]! == 4));
    }

    [Fact]
    public async Task CopyFilesAsync_recordsSourceAndDestination()
    {
        Guid[] ids = [Guid.NewGuid()];

        await CreateSut().CopyFilesAsync(ids, null, _userId, TestContext.Current.CancellationToken);

        _auditWriter.Received(1).Record(Arg.Is<AuditEntry>(e =>
            e.EventCode == AuditEventCode.FilesCopied && e.Metadata!["file_ids"] == (object)ids));
    }
}
