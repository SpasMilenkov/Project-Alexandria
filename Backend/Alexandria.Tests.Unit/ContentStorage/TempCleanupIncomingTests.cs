using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Storage.Cleanup;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Alexandria.Tests.Unit.ContentStorage;

public class TempCleanupIncomingTests
{
    private const string Bucket = "alexandria-temp";

    private readonly IAmazonS3 _s3 = Substitute.For<IAmazonS3>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUploadRepository _uploads = Substitute.For<IUploadRepository>();
    private readonly DateTime _now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private TempCleanupWorker CreateSut()
    {
        _unitOfWork.Uploads.Returns(_uploads);

        var services = new ServiceCollection();
        services.AddScoped(_ => _unitOfWork);

        var config = Substitute.For<IOptions<S3Config>>();
        config.Value.Returns(new S3Config());

        return new TempCleanupWorker(
            services.BuildServiceProvider(),
            _s3,
            config,
            Substitute.For<ILogger<TempCleanupWorker>>());
    }

    private void SeedIncoming(params (string Key, DateTime LastModified)[] objects)
    {
        _s3.ListObjectsV2Async(
                Arg.Is<ListObjectsV2Request>(r => r.Prefix == "incoming/"),
                Arg.Any<CancellationToken>())
            .Returns(new ListObjectsV2Response
            {
                S3Objects = objects
                    .Select(o => new S3Object { Key = $"incoming/{o.Key}", LastModified = o.LastModified })
                    .ToList()
            });
    }

    private static Upload Row(string key, UploadStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TempObjectKey = key,
        MimeType = "text/plain",
        Status = status
    };

    [Fact]
    public async Task SweepIncomingAsync_deletes_only_objects_past_the_abandoned_age()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();

        SeedIncoming(("stale", _now.AddHours(-73)), ("fresh", _now.AddHours(-71)));
        _uploads.GetByTempKeysAsync(Arg.Any<IEnumerable<string>>(), ct).Returns([]);

        var deleted = await sut.SweepIncomingAsync(Bucket, _now, ct);

        deleted.Should().Be(1);
        await _s3.Received(1).DeleteObjectAsync(Bucket, "incoming/stale", ct);
        await _s3.DidNotReceive().DeleteObjectAsync(Bucket, "incoming/fresh", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SweepIncomingAsync_retires_unfinished_rows_and_leaves_finished_ones()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();
        var stale = _now.AddHours(-73);

        var inProgress = Row("in-progress", UploadStatus.InProgress);
        var finalizing = Row("finalizing", UploadStatus.Finalizing);
        var finished = Row("finished", UploadStatus.Finished);

        SeedIncoming(("in-progress", stale), ("finalizing", stale), ("finished", stale));
        _uploads.GetByTempKeysAsync(Arg.Any<IEnumerable<string>>(), ct)
            .Returns([inProgress, finalizing, finished]);

        var deleted = await sut.SweepIncomingAsync(Bucket, _now, ct);

        deleted.Should().Be(3);
        inProgress.Status.Should().Be(UploadStatus.Interrupted);
        finalizing.Status.Should().Be(UploadStatus.Interrupted);
        finished.Status.Should().Be(UploadStatus.Finished);
        await _unitOfWork.Received(1).SaveChangesAsync(ct);
    }

    [Fact]
    public async Task SweepBatchAsync_stranded_finalizing_row_is_swept_like_an_abandoned_upload()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = CreateSut();

        var stranded = Row("stranded", UploadStatus.Finalizing);
        stranded.CreatedAt = _now.AddHours(-73);

        var active = Row("active", UploadStatus.Finalizing);
        active.CreatedAt = _now.AddMinutes(-5);

        _uploads.GetByTempKeysAsync(Arg.Any<IEnumerable<string>>(), ct).Returns([stranded, active]);

        var contentObjects = Substitute.For<IContentObjectRepository>();
        contentObjects.GetByHashesAsync(Arg.Any<IReadOnlyCollection<byte[]>>(), ct).Returns([]);
        _unitOfWork.ContentObjects.Returns(contentObjects);

        var (deleted, _) = await sut.SweepBatchAsync(Bucket, ["stranded", "active"], _now, ct);

        deleted.Should().Be(1);
        stranded.Status.Should().Be(UploadStatus.Interrupted);
        active.Status.Should().Be(UploadStatus.Finalizing);
        await _s3.Received(1).DeleteObjectAsync(Bucket, "content/stranded", ct);
    }
}
