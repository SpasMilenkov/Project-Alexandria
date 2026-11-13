using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Services.Storage.Static;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.StaticAssets;

public class UploadAvatarTests
{
    private const string Bucket = "images";

    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly StaticAssetService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public UploadAvatarTests()
    {
        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config { ImagesBucket = Bucket });

        _s3.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());
        _s3.DeleteObjectsAsync(Arg.Any<DeleteObjectsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectsResponse());

        _sut = new StaticAssetService(
            _s3,
            Substitute.For<IAmazonS3>(),
            Substitute.For<IUnitOfWork>(),
            config);
    }

    private static byte[] WebpBytes(int length = 64)
    {
        var bytes = new byte[length];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        return bytes;
    }

    private string KeyOf(Guid avatarId) => $"avatars/{_userId}/{avatarId}";

    private void BucketHolds(params string[] keys)
    {
        _s3.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns(new ListObjectsV2Response
            {
                S3Objects = keys.Select(key => new S3Object { Key = key }).ToList(),
            });
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public async Task UploadAvatarAsync_supportedImage_storesUnderOwnerPrefix(string contentType)
    {
        var bytes = contentType switch
        {
            "image/jpeg" => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            "image/png" => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            _ => WebpBytes(),
        };

        var avatarId = await _sut.UploadAvatarAsync(
            new MemoryStream(bytes), _userId, TestContext.Current.CancellationToken);

        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == Bucket && r.Key == KeyOf(avatarId) && r.ContentType == contentType),
            Arg.Any<CancellationToken>());
    }

    private static byte[] GifBytes(int width, int height, int length = 64)
    {
        var bytes = new byte[length];
        "GIF89a"u8.CopyTo(bytes);
        bytes[6] = (byte)(width & 0xFF);
        bytes[7] = (byte)(width >> 8);
        bytes[8] = (byte)(height & 0xFF);
        bytes[9] = (byte)(height >> 8);
        return bytes;
    }

    [Fact]
    public async Task UploadAvatarAsync_gifWithinLimits_isStoredAsGif()
    {
        var bytes = GifBytes(512, 512, (int)StaticAssetService.MaxAvatarBytes + 1);

        var avatarId = await _sut.UploadAvatarAsync(
            new MemoryStream(bytes), _userId, TestContext.Current.CancellationToken);

        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r.Key == KeyOf(avatarId) && r.ContentType == "image/gif"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1025, 100)]
    [InlineData(100, 4000)]
    public async Task UploadAvatarAsync_oversizedGifDimensions_isRejectedWithoutStore(int width, int height)
    {
        var act = async () => await _sut.UploadAvatarAsync(
            new MemoryStream(GifBytes(width, height)), _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _s3.DidNotReceive().PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAvatarAsync_gifOverItsCap_isRejectedWithoutStore()
    {
        var bytes = GifBytes(512, 512, (int)StaticAssetService.MaxGifAvatarBytes + 1);

        var act = async () => await _sut.UploadAvatarAsync(
            new MemoryStream(bytes), _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _s3.DidNotReceive().PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAvatarAsync_stillImageOverCap_isRejectedWithoutStore()
    {
        var bytes = WebpBytes((int)StaticAssetService.MaxAvatarBytes + 1);

        var act = async () => await _sut.UploadAvatarAsync(
            new MemoryStream(bytes), _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _s3.DidNotReceive().PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAvatarsExceptAsync_keepsOnlyTheCurrentAvatar()
    {
        var current = Guid.NewGuid();
        var stale = Guid.NewGuid();
        BucketHolds(KeyOf(current), KeyOf(stale));

        var removed = await _sut.DeleteAvatarsExceptAsync(
            _userId, current, TestContext.Current.CancellationToken);

        removed.Should().Be(1);
        await _s3.Received(1).ListObjectsV2Async(
            Arg.Is<ListObjectsV2Request>(r => r.BucketName == Bucket && r.Prefix == $"avatars/{_userId}/"),
            Arg.Any<CancellationToken>());
        await _s3.Received(1).DeleteObjectsAsync(
            Arg.Is<DeleteObjectsRequest>(r => r.Objects.Count == 1 && r.Objects[0].Key == KeyOf(stale)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAvatarsExceptAsync_withoutKeep_removesEverything()
    {
        BucketHolds(KeyOf(Guid.NewGuid()), KeyOf(Guid.NewGuid()));

        var removed = await _sut.DeleteAvatarsExceptAsync(
            _userId, keepAvatarId: null, TestContext.Current.CancellationToken);

        removed.Should().Be(2);
    }

    [Fact]
    public async Task DeleteAvatarsExceptAsync_nothingStale_sendsNoDelete()
    {
        var current = Guid.NewGuid();
        BucketHolds(KeyOf(current));

        var removed = await _sut.DeleteAvatarsExceptAsync(
            _userId, current, TestContext.Current.CancellationToken);

        removed.Should().Be(0);
        await _s3.DidNotReceive().DeleteObjectsAsync(
            Arg.Any<DeleteObjectsRequest>(), Arg.Any<CancellationToken>());
    }
}
