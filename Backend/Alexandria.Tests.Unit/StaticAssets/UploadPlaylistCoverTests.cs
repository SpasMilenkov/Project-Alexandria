using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Exceptions.Playlist;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Services.Storage.Static;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.StaticAssets;

public class UploadPlaylistCoverTests
{
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPlaylistRepository _playlists = Substitute.For<IPlaylistRepository>();
    private readonly StaticAssetService _sut;
    private readonly Guid _playlistId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public UploadPlaylistCoverTests()
    {
        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        _unitOfWork.Playlists.Returns(_playlists);
        _playlists.ExistsAsync(Arg.Any<Expression<Func<Playlist, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(true);

        _s3.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        _sut = new StaticAssetService(
            _s3,
            Substitute.For<IAmazonS3>(),
            _unitOfWork,
            config);
    }

    [Fact]
    public async Task owned_playlist_stores_under_stable_gate_key()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };

        var key = await _sut.UploadPlaylistCoverAsync(
            new MemoryStream(jpeg), "image/jpeg", _playlistId, _userId,
            TestContext.Current.CancellationToken);

        key.Should().Be($"covers/{_playlistId}");
        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r.Key == key && r.ContentType == "image/jpeg"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task foreign_playlist_throws_not_found_without_store()
    {
        _playlists.ExistsAsync(Arg.Any<Expression<Func<Playlist, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(false);

        var act = async () => await _sut.UploadPlaylistCoverAsync(
            new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF }), "image/jpeg",
            _playlistId, _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<PlaylistNotFoundException>();
        await _s3.DidNotReceive().PutObjectAsync(
            Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task oversize_cover_fails_closed_without_store()
    {
        var bytes = new byte[4 * 1024 * 1024 + 1];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;

        var act = async () => await _sut.UploadPlaylistCoverAsync(
            new MemoryStream(bytes), "image/jpeg", _playlistId, _userId,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _s3.DidNotReceive().PutObjectAsync(
            Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task disallowed_type_and_magic_mismatch_fail_closed_without_store()
    {
        var jpeg = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });

        var wrongType = async () => await _sut.UploadPlaylistCoverAsync(
            jpeg, "image/svg+xml", _playlistId, _userId, TestContext.Current.CancellationToken);
        await wrongType.Should().ThrowAsync<InvalidOperationException>();

        var pngAsJpeg = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var mismatch = async () => await _sut.UploadPlaylistCoverAsync(
            pngAsJpeg, "image/jpeg", _playlistId, _userId, TestContext.Current.CancellationToken);
        await mismatch.Should().ThrowAsync<InvalidOperationException>();

        await _s3.DidNotReceive().PutObjectAsync(
            Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }
}