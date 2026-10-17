using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Services.Storage.Static;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.StaticAssets;

public class UploadBackgroundImageTests
{
    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly StaticAssetService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public UploadBackgroundImageTests()
    {
        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        _s3.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        _sut = new StaticAssetService(
            _s3,
            Substitute.For<IAmazonS3>(),
            Substitute.For<IUnitOfWork>(),
            config);
    }

    private static byte[] JpegBytes(int length = 64)
    {
        var bytes = new byte[length];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    public async Task valid_image_stores_under_user_namespaced_key(string contentType)
    {
        var bytes = contentType switch
        {
            "image/jpeg" => JpegBytes(),
            "image/png" => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            "image/gif" => new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 },
            _ => new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 },
        };

        var key = await _sut.UploadBackgroundImageAsync(
            new MemoryStream(bytes), contentType, _userId, TestContext.Current.CancellationToken);

        key.Should().StartWith($"background_images/{_userId}/");
        await _s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r.Key == key && r.ContentType == contentType),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task oversize_upload_fails_closed_without_store()
    {
        var bytes = JpegBytes(4 * 1024 * 1024 + 1);

        var act = async () => await _sut.UploadBackgroundImageAsync(
            new MemoryStream(bytes), "image/jpeg", _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _s3.DidNotReceive().PutObjectAsync(
            Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task disallowed_type_fails_closed_without_store()
    {
        var act = async () => await _sut.UploadBackgroundImageAsync(
            new MemoryStream(JpegBytes()), "image/svg+xml", _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _s3.DidNotReceive().PutObjectAsync(
            Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task magic_mismatch_fails_closed_without_store()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        var act = async () => await _sut.UploadBackgroundImageAsync(
            new MemoryStream(png), "image/jpeg", _userId, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _s3.DidNotReceive().PutObjectAsync(
            Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
    }
}