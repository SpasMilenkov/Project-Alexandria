using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Links;
using Alexandria.Services.Storage.Links;
using AwesomeAssertions;
using NSubstitute;

namespace Alexandria.Tests.Unit.Notes;

public class LinkTargetServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IDirectoryRepository _directories = Substitute.For<IDirectoryRepository>();
    private readonly ITagRepository _tags = Substitute.For<ITagRepository>();
    private readonly IPlaylistRepository _playlists = Substitute.For<IPlaylistRepository>();

    public LinkTargetServiceTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.Directories.Returns(_directories);
        _unitOfWork.Tags.Returns(_tags);
        _unitOfWork.Playlists.Returns(_playlists);

        _files.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([]);
        _directories.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([]);
        _tags.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([]);
        _playlists.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private LinkTargetService CreateSut() => new(_unitOfWork);

    [Fact]
    public async Task ResolveAsync_mixedKinds_returnsEachTargetInRequestOrder()
    {
        var fileId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var playlistId = Guid.NewGuid();

        _files.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([Available(LinkTargetKind.File, fileId, "Trip.md")]);
        _directories.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([Available(LinkTargetKind.Folder, folderId, "Paris")]);
        _tags.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([Available(LinkTargetKind.Tag, tagId, "Vacation")]);
        _playlists.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([Available(LinkTargetKind.Playlist, playlistId, "Road trip")]);

        var result = await CreateSut().ResolveAsync(
            [
                new LinkTargetRef(LinkTargetKind.Playlist, playlistId),
                new LinkTargetRef(LinkTargetKind.File, fileId),
                new LinkTargetRef(LinkTargetKind.Tag, tagId),
                new LinkTargetRef(LinkTargetKind.Folder, folderId),
            ],
            _userId, TestContext.Current.CancellationToken);

        result.Select(target => target.Name).Should().Equal("Road trip", "Trip.md", "Vacation", "Paris");
        result.Should().OnlyContain(target => target.State == LinkTargetState.Available);
    }

    [Fact]
    public async Task ResolveAsync_targetTheRepositoryDoesNotReturn_isUnavailableWithoutName()
    {
        var missing = new LinkTargetRef(LinkTargetKind.File, Guid.NewGuid());

        var result = await CreateSut().ResolveAsync([missing], _userId, TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new LinkTargetDto
        {
            Kind = LinkTargetKind.File,
            Id = missing.Id,
            State = LinkTargetState.Unavailable,
        });
    }

    [Fact]
    public async Task ResolveAsync_sameIdUnderTwoKinds_isNotMatchedAcrossKinds()
    {
        var id = Guid.NewGuid();

        _files.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([Available(LinkTargetKind.File, id, "Trip.md")]);

        var result = await CreateSut().ResolveAsync(
            [new LinkTargetRef(LinkTargetKind.Folder, id), new LinkTargetRef(LinkTargetKind.File, id)],
            _userId, TestContext.Current.CancellationToken);

        result.Select(target => target.State).Should()
            .Equal(LinkTargetState.Unavailable, LinkTargetState.Available);
    }

    [Fact]
    public async Task ResolveAsync_repeatedTarget_isLookedUpAndReturnedOnce()
    {
        var target = new LinkTargetRef(LinkTargetKind.File, Guid.NewGuid());

        var result = await CreateSut().ResolveAsync([target, target], _userId, TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        await _files.Received(1).GetLinkTargetsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1), _userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_onlyFiles_queriesNoOtherRepository()
    {
        await CreateSut().ResolveAsync(
            [new LinkTargetRef(LinkTargetKind.File, Guid.NewGuid())], _userId, TestContext.Current.CancellationToken);

        await _directories.DidNotReceiveWithAnyArgs().GetLinkTargetsAsync(default!, default, default);
        await _tags.DidNotReceiveWithAnyArgs().GetLinkTargetsAsync(default!, default, default);
        await _playlists.DidNotReceiveWithAnyArgs().GetLinkTargetsAsync(default!, default, default);
    }

    [Fact]
    public async Task ResolveAsync_trashedTarget_keepsItsTrashedState()
    {
        var id = Guid.NewGuid();

        _directories.GetLinkTargetsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns([
                new LinkTargetDto
                {
                    Kind = LinkTargetKind.Folder,
                    Id = id,
                    State = LinkTargetState.Trashed,
                    Name = "Old",
                },
            ]);

        var result = await CreateSut().ResolveAsync(
            [new LinkTargetRef(LinkTargetKind.Folder, id)], _userId, TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.State.Should().Be(LinkTargetState.Trashed);
    }

    private static LinkTargetDto Available(LinkTargetKind kind, Guid id, string name) => new()
    {
        Kind = kind,
        Id = id,
        State = LinkTargetState.Available,
        Name = name,
    };
}
