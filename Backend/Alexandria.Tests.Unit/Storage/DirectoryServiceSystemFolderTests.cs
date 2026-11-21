using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Exceptions.Directories;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Directories;
using AwesomeAssertions;
using NSubstitute;
using Directory = Alexandria.Data.Models.Directory;

namespace Alexandria.Tests.Unit.Storage;

public class DirectoryServiceSystemFolderTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDirectoryRepository _directories = Substitute.For<IDirectoryRepository>();

    private readonly Directory _notes;

    public DirectoryServiceSystemFolderTests()
    {
        _unitOfWork.Directories.Returns(_directories);

        _notes = new Directory
        {
            Id = Guid.NewGuid(),
            Name = "Notes",
            OwnerId = _userId,
            SystemKind = DirectorySystemKind.Notes,
        };

        _directories.GetByIdAsync(_notes.Id, Arg.Any<CancellationToken>()).Returns(_notes);
    }

    private DirectoryService CreateSut() => new(_unitOfWork);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteDirectory_notesFolder_isRefusedAndNothingIsTrashed(bool force)
    {
        var act = () => CreateSut().DeleteDirectoryAsync(_notes.Id, _userId, force, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SystemDirectoryProtectedException>();
        _directories.DidNotReceive().Remove(Arg.Any<Directory>());
        await _directories.DidNotReceive()
            .DeleteDirectoryTreeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateDirectory_notesFolder_isRefusedAndNameIsKept()
    {
        var act = () => CreateSut().UpdateDirectoryAsync(_notes.Id, "Journal", _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SystemDirectoryProtectedException>();
        _notes.Name.Should().Be("Notes");
        _directories.DidNotReceive().Update(Arg.Any<Directory>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveDirectory_selectionIncludingNotes_isRefusedAndNothingMoves()
    {
        _directories.FirstOrDefaultAsync(Arg.Any<Expression<Func<Directory, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Expression<Func<Directory, bool>>>().Compile()(_notes) ? _notes : null);

        var act = () => CreateSut().MoveDirectoryAsync([Guid.NewGuid(), _notes.Id], Guid.NewGuid(), _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SystemDirectoryProtectedException>();
        await _directories.DidNotReceive().MoveDirectoriesAsync(Arg.Any<Guid[]>(), Arg.Any<Guid?>(), Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveDirectory_ordinaryFolders_moveAsBefore()
    {
        var ids = new[] { Guid.NewGuid() };

        await CreateSut().MoveDirectoryAsync(ids, null, _userId, TestContext.Current.CancellationToken);

        await _directories.Received(1).MoveDirectoriesAsync(ids, null, _userId, Arg.Any<CancellationToken>());
    }
}
