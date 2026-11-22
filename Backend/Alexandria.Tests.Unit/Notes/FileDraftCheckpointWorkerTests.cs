using Alexandria.Common;
using Alexandria.Common.Audit;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Dto.Files;
using Alexandria.Services.Storage.Notes;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Alexandria.Tests.Unit.Notes;

public class FileDraftCheckpointWorkerTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileDraftRepository _drafts = Substitute.For<IFileDraftRepository>();
    private readonly IFileDraftService _draftService = Substitute.For<IFileDraftService>();

    // Each from a different owner, so a mixed-up owner shows in the assertions.
    private readonly IdleFileDraft _first = NewIdleDraft();
    private readonly IdleFileDraft _second = NewIdleDraft();
    private readonly IdleFileDraft _third = NewIdleDraft();

    public FileDraftCheckpointWorkerTests()
    {
        _unitOfWork.FileDrafts.Returns(_drafts);

        _drafts.GetContentAsync(_first.FileId, _first.Revision, Arg.Any<CancellationToken>())
            .Returns("first, left behind");
        _drafts.GetContentAsync(_second.FileId, _second.Revision, Arg.Any<CancellationToken>())
            .Returns("second, left behind");
        _drafts.GetContentAsync(_third.FileId, _third.Revision, Arg.Any<CancellationToken>())
            .Returns("third, left behind");

        _draftService.CheckpointAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => new FileContentSaveResult
            {
                FileId = call.ArgAt<Guid>(0),
                VersionId = Guid.NewGuid(),
                VersionNumber = 2,
                Size = 1,
                FileName = "note.md",
                MimeType = "text/markdown",
                DirectoryId = null,
                Created = false,
                Unchanged = false,
            });
    }

    private static IdleFileDraft NewIdleDraft() => new()
    {
        FileId = Guid.NewGuid(),
        BaseVersionId = Guid.NewGuid(),
        Revision = Guid.NewGuid(),
        OwnerId = Guid.NewGuid(),
    };

    private FileDraftCheckpointWorker CreateSut()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _unitOfWork);
        services.AddScoped(_ => _draftService);
        services.AddScoped<AuditContext>();

        return new FileDraftCheckpointWorker(services.BuildServiceProvider(),
            Substitute.For<ILogger<FileDraftCheckpointWorker>>());
    }

    [Fact]
    public async Task Sweep_idleDrafts_becomeVersionsForTheirOwnersAtTheirRevision()
    {
        _drafts.GetIdleAsync(Now - FileDraftCheckpointWorker.IdleAge, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([_first, _second]);

        var checkpointed = await CreateSut().SweepAsync(Now, TestContext.Current.CancellationToken);

        checkpointed.Should().Be(2);
        await _draftService.Received(1).CheckpointAsync(_first.FileId, _first.BaseVersionId, _first.Revision,
            "first, left behind", _first.OwnerId, Arg.Any<CancellationToken>());
        await _draftService.Received(1).CheckpointAsync(_second.FileId, _second.BaseVersionId, _second.Revision,
            "second, left behind", _second.OwnerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_draftWrittenSinceTheBatchWasRead_isLeftToItsEditor()
    {
        _drafts.GetIdleAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([_first, _second]);
        _drafts.GetContentAsync(_first.FileId, _first.Revision, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var checkpointed = await CreateSut().SweepAsync(Now, TestContext.Current.CancellationToken);

        checkpointed.Should().Be(1);
        await _draftService.DidNotReceive().CheckpointAsync(_first.FileId, Arg.Any<Guid>(), Arg.Any<Guid?>(),
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_draftThatMovedOnOrFails_isSkippedWithoutStoppingTheBatch()
    {
        _drafts.GetIdleAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([_first, _second, _third]);
        _draftService.CheckpointAsync(_first.FileId, Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileVersionConflictException(_first.FileId, _first.BaseVersionId, Guid.NewGuid()));
        _draftService.CheckpointAsync(_second.FileId, Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string>(),
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("storage down"));

        var checkpointed = await CreateSut().SweepAsync(Now, TestContext.Current.CancellationToken);

        checkpointed.Should().Be(1);
        await _draftService.Received(1).CheckpointAsync(_third.FileId, Arg.Any<Guid>(), Arg.Any<Guid?>(),
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
