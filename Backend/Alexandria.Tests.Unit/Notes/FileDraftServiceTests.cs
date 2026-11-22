using System.Text;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.FileVersions;
using Alexandria.Common.Notes;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Dto.Files;
using Alexandria.Services.Storage.Notes;
using AwesomeAssertions;
using Blake3;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.Notes;

public class FileDraftServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly IFileDraftRepository _drafts = Substitute.For<IFileDraftRepository>();
    private readonly IContentStorageService _contentStorage = Substitute.For<IContentStorageService>();
    private readonly INoteService _noteService = Substitute.For<INoteService>();
    private readonly IPolicyDispatcher _policyDispatcher = Substitute.For<IPolicyDispatcher>();

    private readonly File _file;
    private readonly FileVersion _base;
    // Stored only in the tests that make the drafts repository return it.
    private readonly FileDraft _draft;
    private readonly FileContentSaveResult _saved;
    private readonly List<FileDraft> _addedDrafts = [];

    public FileDraftServiceTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.FileVersions.Returns(_versions);
        _unitOfWork.FileDrafts.Returns(_drafts);

        _base = new FileVersion
        {
            Id = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            ContentHash = Digest("stored text"),
            Size = 11,
            VersionNumber = 2,
            MimeType = "text/markdown",
        };

        _file = new File
        {
            Id = _base.FileId,
            Name = "Untitled.md",
            MimeType = "text/markdown",
            OwnerId = _userId,
            CurrentVersionId = _base.Id,
        };

        _draft = new FileDraft
        {
            FileId = _file.Id,
            BaseVersionId = _base.Id,
            Revision = Guid.NewGuid(),
            Content = "earlier draft",
        };

        _saved = new FileContentSaveResult
        {
            FileId = _file.Id,
            VersionId = Guid.NewGuid(),
            VersionNumber = 3,
            Size = 10,
            FileName = _file.Name,
            MimeType = "text/markdown",
            DirectoryId = null,
            Created = false,
            Unchanged = false,
        };

        _files.GetForUpdateAsync(_file.Id, _userId, Arg.Any<CancellationToken>()).Returns(_file);
        _versions.GetByIdAsync(_base.Id, Arg.Any<CancellationToken>()).Returns(_base);
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns((FileDraft?)null);
        _drafts.AddAsync(Arg.Do<FileDraft>(_addedDrafts.Add), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<FileDraft>());
    }

    private FileDraftService CreateSut() => new(_unitOfWork, _contentStorage, _noteService, _policyDispatcher,
        Substitute.For<ILogger<FileDraftService>>());

    private static byte[] Digest(string text) => Hasher.Hash(Encoding.UTF8.GetBytes(text)).AsSpan().ToArray();

    [Fact]
    public async Task Save_noDraftYet_storesOneAtANewRevision()
    {
        var saved = await CreateSut().SaveAsync(_file.Id, _base.Id, null, "# Typed", _userId,
            TestContext.Current.CancellationToken);

        var draft = _addedDrafts.Should().ContainSingle().Subject;
        draft.Content.Should().Be("# Typed");
        draft.BaseVersionId.Should().Be(_base.Id);
        saved.Revision.Should().Be(draft.Revision);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_encryptedBase_isRefusedAndStoresNothing()
    {
        _base.IsEncrypted = true;

        var act = () => CreateSut().SaveAsync(_file.Id, _base.Id, null, "# Typed", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileContentNotEditableException>();
        _addedDrafts.Should().BeEmpty();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    // The idle sweep would otherwise turn this draft into a version of the video an hour later.
    [Fact]
    public async Task Save_nonTextBase_isRefusedAndStoresNothing()
    {
        _base.MimeType = "video/mp4";

        var act = () => CreateSut().SaveAsync(_file.Id, _base.Id, null, "# Typed", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileContentNotEditableException>();
        _addedDrafts.Should().BeEmpty();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_baseTooLargeToOpen_isRefusedAndStoresNothing()
    {
        _base.Size = TextContentLimits.MaxBytes + 1;

        var act = () => CreateSut().SaveAsync(_file.Id, _base.Id, null, "# Typed", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileContentNotEditableException>();
        _addedDrafts.Should().BeEmpty();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_draftAtTheExpectedRevision_replacesItAtANewRevision()
    {
        var revision = _draft.Revision;
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        var saved = await CreateSut().SaveAsync(_file.Id, _base.Id, revision, "# Typed more", _userId,
            TestContext.Current.CancellationToken);

        _draft.Content.Should().Be("# Typed more");
        _draft.Revision.Should().NotBe(revision);
        saved.Revision.Should().Be(_draft.Revision);
        _drafts.Received(1).Update(_draft);
        _addedDrafts.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_draftAtAnotherRevision_isRefusedAndRolledBack()
    {
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        var act = () => CreateSut().SaveAsync(_file.Id, _base.Id, Guid.NewGuid(), "# Mine", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileDraftConflictException>();
        _draft.Content.Should().Be("earlier draft");
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_anotherTabsDraftAndNoneSeenHere_isRefused()
    {
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        var act = () => CreateSut().SaveAsync(_file.Id, _base.Id, null, "# Mine", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileDraftConflictException>();
        _addedDrafts.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_baseNoLongerCurrent_isRefused()
    {
        _file.CurrentVersionId = Guid.NewGuid();

        var act = () => CreateSut().SaveAsync(_file.Id, _base.Id, null, "# Typed", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileVersionConflictException>();
        _addedDrafts.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_textBackToTheVersion_removesTheDraft()
    {
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        var saved = await CreateSut().SaveAsync(_file.Id, _base.Id, _draft.Revision, "stored text", _userId,
            TestContext.Current.CancellationToken);

        saved.Revision.Should().BeNull();
        _drafts.Received(1).Remove(_draft);
        await _noteService.DidNotReceive().NameUntitledNoteAsync(Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_tooLarge_isRefusedBeforeTheLock()
    {
        var act = () => CreateSut().SaveAsync(_file.Id, _base.Id, null, new string('a', 1_000_001), _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileContentNotEditableException>();
        await _files.DidNotReceive().GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_trashedFile_isNotFound()
    {
        _file.DeletedAt = DateTime.UtcNow;

        var act = () => CreateSut().SaveAsync(_file.Id, _base.Id, null, "# Typed", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileRecordNotFoundException>();
    }

    [Fact]
    public async Task Save_storedDraft_namesAnUntitledNoteAfterIt()
    {
        _noteService.NameUntitledNoteAsync(_file.Id, "# Trip to Paris\n\nDay one", _userId,
                Arg.Any<CancellationToken>())
            .Returns("Trip to Paris.md");

        var saved = await CreateSut().SaveAsync(_file.Id, _base.Id, null, "# Trip to Paris\n\nDay one", _userId,
            TestContext.Current.CancellationToken);

        saved.RenamedTo.Should().Be("Trip to Paris.md");
    }

    // Every autosave of a named note would otherwise lock the file row a second time for nothing.
    [Fact]
    public async Task Save_storedDraftOfANamedNote_neverTriesToRenameIt()
    {
        _file.Name = "Trip to Paris.md";

        var saved = await CreateSut().SaveAsync(_file.Id, _base.Id, null, "# Day two\n\nLouvre", _userId,
            TestContext.Current.CancellationToken);

        saved.Revision.Should().NotBeNull();
        saved.RenamedTo.Should().BeNull();
        await _noteService.DidNotReceive().NameUntitledNoteAsync(Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Discard_atTheStoredRevision_removesTheDraft()
    {
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        var discarded = await CreateSut().DiscardAsync(_file.Id, _draft.Revision, _userId,
            TestContext.Current.CancellationToken);

        discarded.Should().BeTrue();
        _drafts.Received(1).Remove(_draft);
    }

    [Fact]
    public async Task Discard_atAnOlderRevision_keepsTheNewerDraft()
    {
        _drafts.GetByIdAsync(_file.Id, Arg.Any<CancellationToken>()).Returns(_draft);

        var discarded = await CreateSut().DiscardAsync(_file.Id, Guid.NewGuid(), _userId,
            TestContext.Current.CancellationToken);

        discarded.Should().BeFalse();
        _drafts.DidNotReceive().Remove(_draft);
    }

    [Fact]
    public async Task Checkpoint_newVersion_namesTheNoteAndRunsTheFoldersPolicies()
    {
        _contentStorage.SaveTextContentAsync(_file.Id, _base.Id, _draft.Revision, "# Paris", _userId,
                Arg.Any<CancellationToken>())
            .Returns(_saved);
        _noteService.NameUntitledNoteAsync(_file.Id, "# Paris", _userId, Arg.Any<CancellationToken>())
            .Returns("Paris.md");

        var result = await CreateSut().CheckpointAsync(_file.Id, _base.Id, _draft.Revision, "# Paris", _userId,
            TestContext.Current.CancellationToken);

        result.FileName.Should().Be("Paris.md");
        await _policyDispatcher.Received(1).DispatchAsync(
            Arg.Is<FileFinalizedEvent>(e => e.VersionId == _saved.VersionId && e.FileName == "Paris.md" && e.IsNewVersion),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkpoint_unchangedText_startsNothing()
    {
        _contentStorage.SaveTextContentAsync(_file.Id, _base.Id, null, "stored text", _userId,
                Arg.Any<CancellationToken>())
            .Returns(_saved with { Unchanged = true });

        await CreateSut().CheckpointAsync(_file.Id, _base.Id, null, "stored text", _userId,
            TestContext.Current.CancellationToken);

        await _policyDispatcher.DidNotReceive().DispatchAsync(Arg.Any<FileFinalizedEvent>(), Arg.Any<CancellationToken>());
        await _noteService.DidNotReceive().NameUntitledNoteAsync(Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkpoint_policiesFail_stillReportsTheSavedVersion()
    {
        _contentStorage.SaveTextContentAsync(_file.Id, _base.Id, null, "# Paris", _userId,
                Arg.Any<CancellationToken>())
            .Returns(_saved);
        _policyDispatcher.DispatchAsync(Arg.Any<FileFinalizedEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var result = await CreateSut().CheckpointAsync(_file.Id, _base.Id, null, "# Paris", _userId,
            TestContext.Current.CancellationToken);

        result.VersionId.Should().Be(_saved.VersionId);
    }
}
