using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Tests.Common.Builders;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MediaMetadataEntity = Alexandria.Data.Models.MediaMetadata;
using FileEntity = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.FileService;

public class UpdateFileMetadataTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IMediaMetadataRepository _metadata = Substitute.For<IMediaMetadataRepository>();
    private readonly Services.Storage.FileService _sut;
    private readonly Guid _fileId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateFileMetadataTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.MediaMetadata.Returns(_metadata);

        _sut = new Services.Storage.FileService(
            _unitOfWork,
            Substitute.For<IDirectoryService>(),
            Substitute.For<ILogger<Services.Storage.FileService>>());
    }

    private FileEntity OwnedFile()
        => new FileBuilder()
            .WithId(_fileId)
            .WithOwner(_userId)
            .Build();

    private void GivenFile(FileEntity file)
    {
        var ct = TestContext.Current.CancellationToken;
        _files.GetByIdAsync(_fileId, ct).Returns(file);
        _files.UpdateAsync(Arg.Any<FileEntity>(), ct).Returns(file);
    }

    private void GivenMetadata(MediaMetadataEntity? metadata)
    {
        var ct = TestContext.Current.CancellationToken;
        _metadata
            .FirstOrDefaultAsync(
                Arg.Any<Expression<Func<MediaMetadataEntity, bool>>>(),
                ct)
            .Returns(metadata);
    }

    [Fact]
    public async Task UpdateFileMetadata_setsAlbumAndYear_onExistingRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = OwnedFile();
        GivenFile(file);
        var metadata = new MediaMetadataEntity { FileId = _fileId, Title = "T" };
        GivenMetadata(metadata);

        var result = await _sut.UpdateFileMetadataAsync(
            _fileId, _userId, newAlbum: "New Album", newYear: "2024", ct: ct);

        metadata.Album.Should().Be("New Album");
        metadata.Year.Should().Be("2024");
        metadata.Title.Should().Be("T");
        metadata.UpdatedBy.Should().Be(_userId);
        await _metadata.Received(1).UpdateAsync(metadata, ct);
        await _metadata.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
        result.MediaMetadata.Should().Be(metadata);
    }

    [Fact]
    public async Task UpdateFileMetadata_missingRow_createsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = OwnedFile();
        GivenFile(file);
        GivenMetadata(null);

        var result = await _sut.UpdateFileMetadataAsync(
            _fileId, _userId, newArtist: "New Artist", newAlbum: "New Album", ct: ct);

        await _metadata.Received(1).CreateAsync(
            Arg.Is<MediaMetadataEntity>(m =>
                m.FileId == _fileId
                && m.Artist == "New Artist"
                && m.Album == "New Album"
                && m.UpdatedBy == _userId),
            ct);
        await _metadata.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        result.MediaMetadata.Should().NotBeNull();
        result.MediaMetadata!.Artist.Should().Be("New Artist");
    }

    [Fact]
    public async Task UpdateFileMetadata_onlyName_skipsMetadata()
    {
        var ct = TestContext.Current.CancellationToken;
        var file = OwnedFile();
        GivenFile(file);

        var result = await _sut.UpdateFileMetadataAsync(_fileId, _userId, newName: "renamed.mp3", ct: ct);

        result.Name.Should().Be("renamed.mp3");
        await _metadata.DidNotReceiveWithAnyArgs().FirstOrDefaultAsync(default!, default);
        await _metadata.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
        await _metadata.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task UpdateFileMetadata_unknownFile_throws()
    {
        var ct = TestContext.Current.CancellationToken;
        _files.GetByIdAsync(_fileId, ct).Returns((FileEntity?)null);

        var act = () => _sut.UpdateFileMetadataAsync(_fileId, _userId, newTitle: "T", ct: ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task BulkUpdateFileMetadata_mixedBatch_reportsPartialSuccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var ownedId = Guid.NewGuid();
        var foreignId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var owned = new FileBuilder().WithId(ownedId).WithOwner(_userId).Build();
        var foreign = new FileBuilder().WithId(foreignId).WithOwner(Guid.NewGuid()).Build();

        _files.GetByIdAsync(ownedId, ct).Returns(owned);
        _files.GetByIdAsync(foreignId, ct).Returns(foreign);
        _files.GetByIdAsync(missingId, ct).Returns((FileEntity?)null);
        _files.UpdateAsync(Arg.Any<FileEntity>(), ct).Returns(owned);
        _metadata
            .FirstOrDefaultAsync(
                Arg.Any<Expression<Func<MediaMetadataEntity, bool>>>(),
                ct)
            .Returns((MediaMetadataEntity?)null);

        var results = await _sut.BulkUpdateFileMetadataAsync(
            [ownedId, foreignId, missingId], _userId, newAlbum: "Bulk Album", ct: ct);

        results.Should().HaveCount(3);
        results[0].Should().Be(new FileMetadataUpdateResult(ownedId, true, null));
        results[1].Success.Should().BeFalse();
        results[1].Error.Should().NotBeNullOrEmpty();
        results[2].Success.Should().BeFalse();
        await _metadata.Received(1).CreateAsync(
            Arg.Is<MediaMetadataEntity>(m => m.FileId == ownedId && m.Album == "Bulk Album"),
            ct);
    }

    [Fact]
    public async Task BulkUpdateFileMetadata_emptyList_returnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;

        var results = await _sut.BulkUpdateFileMetadataAsync([], _userId, newTitle: "T", ct: ct);

        results.Should().BeEmpty();
    }
}