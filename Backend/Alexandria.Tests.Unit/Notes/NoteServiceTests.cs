using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Files;
using Alexandria.Common.Exceptions.Notes;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Directories;
using Alexandria.Dto.Files;
using Alexandria.Services.Storage.Notes;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Directory = Alexandria.Data.Models.Directory;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.Notes;

public class NoteServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDirectoryRepository _directories = Substitute.For<IDirectoryRepository>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IDirectoryService _directoryService = Substitute.For<IDirectoryService>();
    private readonly IContentStorageService _contentStorage = Substitute.For<IContentStorageService>();

    private readonly List<Directory> _addedDirectories = [];

    public NoteServiceTests()
    {
        _unitOfWork.Directories.Returns(_directories);
        _unitOfWork.Files.Returns(_files);

        _directories.AddAsync(Arg.Do<Directory>(_addedDirectories.Add), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Directory>());

        _files.FindAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _contentStorage.CreateTextFileAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string>(), _userId,
                Arg.Any<CancellationToken>())
            .Returns(call => Created(call.ArgAt<string>(0), call.ArgAt<Guid?>(1)));
    }

    private NoteService CreateSut() =>
        new(_unitOfWork, _directoryService, _contentStorage, Substitute.For<ILogger<NoteService>>());

    private static FileContentSaveResult Created(string fileName, Guid? directoryId) => new()
    {
        FileId = Guid.NewGuid(),
        VersionId = Guid.NewGuid(),
        VersionNumber = 1,
        Size = 0,
        FileName = fileName,
        MimeType = "text/markdown",
        DirectoryId = directoryId,
        Created = true,
        Unchanged = false,
    };

    // Answers every directory lookup by running the query against this one folder.
    private void GivenOnlyRootFolder(Directory directory)
    {
        _directories.FirstOrDefaultAsync(Arg.Any<Expression<Func<Directory, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Expression<Func<Directory, bool>>>().Compile()(directory) ? directory : null);
    }

    private Directory GivenNotesFolder()
    {
        var notes = new Directory
        {
            Id = Guid.NewGuid(),
            Name = "Notes",
            OwnerId = _userId,
            SystemKind = DirectorySystemKind.Notes,
        };

        GivenOnlyRootFolder(notes);

        return notes;
    }

    private void GivenFolderAt(Guid directoryId, params Guid[] pathFromRoot)
    {
        _directories.ExistsAsync(Arg.Any<Expression<Func<Directory, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _directories.GetDirectoryPathAsync(directoryId, Arg.Any<CancellationToken>())
            .Returns(pathFromRoot.Select(id => new PathPartDto(id, "folder")).ToList());
    }

    [Fact]
    public async Task CreateNote_firstNote_createsTheNotesFolderAndAnUntitledNoteInIt()
    {
        var created = await CreateSut().CreateNoteAsync(_userId, null, null, TestContext.Current.CancellationToken);

        var folder = _addedDirectories.Should().ContainSingle().Subject;
        folder.Name.Should().Be(NoteService.NotesFolderName);
        folder.ParentId.Should().BeNull();
        folder.SystemKind.Should().Be(DirectorySystemKind.Notes);

        created.FileName.Should().Be("Untitled.md");
        created.DirectoryId.Should().Be(folder.Id);
        await _contentStorage.Received(1).CreateTextFileAsync("Untitled.md", folder.Id, string.Empty, _userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNote_rootFolderAlreadyCalledNotes_marksItInsteadOfAddingAnother()
    {
        var existing = new Directory { Id = Guid.NewGuid(), Name = "Notes", OwnerId = _userId };
        GivenOnlyRootFolder(existing);

        var created = await CreateSut().CreateNoteAsync(_userId, null, "Ideas", TestContext.Current.CancellationToken);

        _addedDirectories.Should().BeEmpty();
        existing.SystemKind.Should().Be(DirectorySystemKind.Notes);
        _directories.Received(1).Update(existing);
        created.DirectoryId.Should().Be(existing.Id);
        created.FileName.Should().Be("Ideas.md");
    }

    [Fact]
    public async Task CreateNote_subfolderOfNotes_createsTheNoteThere()
    {
        var notes = GivenNotesFolder();
        var project = Guid.NewGuid();
        GivenFolderAt(project, notes.Id, project);

        var created = await CreateSut().CreateNoteAsync(_userId, project, "Plan", TestContext.Current.CancellationToken);

        created.DirectoryId.Should().Be(project);
    }

    [Fact]
    public async Task CreateNote_folderOutsideNotes_isRefused()
    {
        GivenNotesFolder();
        var elsewhere = Guid.NewGuid();
        GivenFolderAt(elsewhere, Guid.NewGuid(), elsewhere);

        var act = () => CreateSut().CreateNoteAsync(_userId, elsewhere, "Plan", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotesFolderRequiredException>();
        await _contentStorage.DidNotReceiveWithAnyArgs().CreateTextFileAsync(default!, default, default!, default,
            default);
    }

    [Fact]
    public async Task CreateNote_nameTakenIncludingTrash_picksTheNextNumber()
    {
        var notes = GivenNotesFolder();
        _files.FindAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([new File { Name = "untitled.md", MimeType = "text/markdown", DeletedAt = DateTime.UtcNow }]);

        var created = await CreateSut().CreateNoteAsync(_userId, null, null, TestContext.Current.CancellationToken);

        created.FileName.Should().Be("Untitled 2.md");
        created.DirectoryId.Should().Be(notes.Id);
    }

    [Fact]
    public async Task CreateNote_nameTakenMeanwhile_retriesWithTheNextFreeName()
    {
        GivenNotesFolder();
        var raced = new File { Name = "Untitled.md", MimeType = "text/markdown" };
        _files.FindAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([], [raced]);
        _contentStorage.CreateTextFileAsync("Untitled.md", Arg.Any<Guid?>(), Arg.Any<string>(), _userId,
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNameTakenException("Untitled.md", null));

        var created = await CreateSut().CreateNoteAsync(_userId, null, null, TestContext.Current.CancellationToken);

        created.FileName.Should().Be("Untitled 2.md");
    }

    private File GivenNote(string name)
    {
        var note = new File
        {
            Id = Guid.NewGuid(),
            Name = name,
            MimeType = "text/markdown",
            OwnerId = _userId,
            DirectoryId = Guid.NewGuid(),
        };

        _files.GetForUpdateAsync(note.Id, _userId, Arg.Any<CancellationToken>()).Returns(note);

        return note;
    }

    private void GivenOtherFilesNamed(params string[] names)
    {
        _files.FindAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(names.Select(name => new File { Name = name, MimeType = "text/markdown" }).ToList());
    }

    [Fact]
    public async Task NameUntitledNote_untitledNote_takesItsFirstFinishedLine()
    {
        var note = GivenNote("Untitled 2.md");

        var name = await CreateSut().NameUntitledNoteAsync(note.Id, "# Trip plans\n\nBook the train.", _userId,
            TestContext.Current.CancellationToken);

        name.Should().Be("Trip plans.md");
        note.Name.Should().Be("Trip plans.md");
        _files.Received(1).Update(note);
    }

    [Fact]
    public async Task NameUntitledNote_nameTakenInTheFolder_numbersIt()
    {
        var note = GivenNote("Untitled.md");
        GivenOtherFilesNamed("trip plans.md");

        var name = await CreateSut().NameUntitledNoteAsync(note.Id, "Trip plans\nmore", _userId,
            TestContext.Current.CancellationToken);

        name.Should().Be("Trip plans 2.md");
    }

    [Fact]
    public async Task NameUntitledNote_noteAlreadyNamed_isLeftAlone()
    {
        var note = GivenNote("Packing.md");

        var name = await CreateSut().NameUntitledNoteAsync(note.Id, "# Trip plans\nmore", _userId,
            TestContext.Current.CancellationToken);

        name.Should().BeNull();
        note.Name.Should().Be("Packing.md");
        _files.DidNotReceive().Update(Arg.Any<File>());
    }

    [Fact]
    public async Task NameUntitledNote_firstLineStillBeingTyped_waits()
    {
        var note = GivenNote("Untitled.md");

        var name = await CreateSut().NameUntitledNoteAsync(note.Id, "# Trip pl", _userId,
            TestContext.Current.CancellationToken);

        name.Should().BeNull();
        await _files.DidNotReceive().GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NameUntitledNote_failure_isLoggedAndTheSaveStands()
    {
        var fileId = Guid.NewGuid();
        _files.GetForUpdateAsync(fileId, _userId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database down"));

        var name = await CreateSut().NameUntitledNoteAsync(fileId, "Trip plans\nmore", _userId,
            TestContext.Current.CancellationToken);

        name.Should().BeNull();
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rename_typedName_keepsTheExtensionAndCleansIt()
    {
        var note = GivenNote("notes.markdown");

        var renamed = await CreateSut().RenameAsync(note.Id, "Trip/plans", _userId,
            TestContext.Current.CancellationToken);

        renamed.FileName.Should().Be("Trip plans.markdown");
        note.Name.Should().Be("Trip plans.markdown");
    }

    [Fact]
    public async Task Rename_nameUsedByAnotherFile_isRefusedRatherThanNumbered()
    {
        var note = GivenNote("Untitled.md");
        GivenOtherFilesNamed("Ideas.md");

        var act = () => CreateSut().RenameAsync(note.Id, "ideas", _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileNameTakenException>();
        note.Name.Should().Be("Untitled.md");
        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rename_trashedNote_isNotFound()
    {
        var note = GivenNote("Ideas.md");
        note.DeletedAt = DateTime.UtcNow;

        var act = () => CreateSut().RenameAsync(note.Id, "Plans", _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileRecordNotFoundException>();
    }
}
