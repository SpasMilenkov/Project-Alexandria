using System.Linq.Expressions;
using System.Text;
using Alexandria.Common;
using Alexandria.Common.Config;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Services.Preview;
using Alexandria.Tests.Common.Builders;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class PreviewServiceDispatchTests
{
    private readonly IContentStorageService _storage = Substitute.For<IContentStorageService>();
    private readonly IPreviewArtifactService _artifacts = Substitute.For<IPreviewArtifactService>();
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();
    private readonly IMessageBrokerStatus _broker = Substitute.For<IMessageBrokerStatus>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly IPreviewJobRepository _previewJobs = Substitute.For<IPreviewJobRepository>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly ITextPreviewService _textPreview = Substitute.For<ITextPreviewService>();
    private readonly IArchivePreviewService _archivePreview = Substitute.For<IArchivePreviewService>();
    private readonly PreviewService _sut;
    private readonly ProcessingCapabilities _capabilities = new();

    private readonly Guid _versionId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public PreviewServiceDispatchTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.FileVersions.Returns(_versions);
        _unitOfWork.PreviewJobs.Returns(_previewJobs);
        _unitOfWork.Jobs.Returns(_jobs);
        _broker.IsEnabled.Returns(true);
        _broker.IsAvailable.Returns(true);
        _jobs.AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Job>());
        _previewJobs.AddAsync(Arg.Any<PreviewJob>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<PreviewJob>());

        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        _sut = new PreviewService(
            _storage, _artifacts, _publisher, _broker, _unitOfWork,
            _textPreview, _archivePreview, cache, Options.Create(_capabilities));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task RepublishJobAsync_onlyPublishesWhenTheVersionIsNotDeleted(bool deleted, bool expected)
    {
        var job = new JobBuilder().WithUser(_userId).Build();
        _previewJobs.GetByJobIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(new PreviewJobBuilder().WithJob(job).WithVersion(_versionId).WithUser(_userId).Build());

        var version = new FileVersionBuilder().WithId(_versionId).Build();
        version.DeletedAt = deleted ? DateTime.UtcNow : null;

        // Runs the real predicate, because the row exists either way: files are soft deleted.
        _versions.FirstOrDefaultAsync(Arg.Any<Expression<Func<FileVersion, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => new[] { version }.AsQueryable()
                .FirstOrDefault(call.Arg<Expression<Func<FileVersion, bool>>>()));
        _files.GetMimeTypeByVersionIdAsync(_versionId, Arg.Any<CancellationToken>()).Returns("video/mp4");
        _storage.CategorizeFile("video/mp4").Returns(FileCategory.Video);

        var result = await _sut.RepublishJobAsync(job.Id, TestContext.Current.CancellationToken);

        result.Should().Be(expected);
        await _publisher.Received(expected ? 1 : 0).PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    private File StoredFile(string mimeType) =>
        new FileBuilder().WithMimeType(mimeType).WithOwner(_userId).Build();

    private void StoredImage()
    {
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _artifacts.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile("image/png"));
        _storage.CategorizeFile("image/png").Returns(FileCategory.Image);
    }

    [Theory]
    [InlineData("image/png", FileCategory.Image)]
    [InlineData("application/pdf", FileCategory.Document)]
    public async Task GeneratePreviewAsync_disabled_consumer_does_not_create_or_requeue_work(
        string mimeType, FileCategory category)
    {
        StoredImage();
        _capabilities.DocumentPreviews = false;
        _capabilities.MediaPreviews = false;
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile(mimeType));
        _storage.CategorizeFile(mimeType).Returns(category);
        var job = new JobBuilder().WithStatus(JobStatus.Failed).WithUser(_userId).Build();
        _previewJobs.FirstOrDefaultAsync(Arg.Any<Expression<Func<PreviewJob, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new PreviewJobBuilder().WithJob(job).WithVersion(_versionId).WithUser(_userId).Build());

        await _sut.GeneratePreviewAsync(_versionId, _userId, ct: TestContext.Current.CancellationToken);

        await _jobs.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await _jobs.DidNotReceiveWithAnyArgs().UpdateStatusAsync(default, default, default, default, default);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!);
    }

    [Fact]
    public async Task GeneratePreviewAsync_previews_and_analysis_disabled_still_queues_media_metadata()
    {
        StoredImage();
        _capabilities.MediaPreviews = false;
        _capabilities.AudioAnalysis = false;
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile("audio/mpeg"));
        _storage.CategorizeFile("audio/mpeg").Returns(FileCategory.Audio);

        await _sut.GeneratePreviewAsync(_versionId, _userId, ct: TestContext.Current.CancellationToken);

        await _jobs.Received(1).AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(Arg.Any<byte[]>(), "media.mpeg");
    }

    [Fact]
    public async Task GeneratePreviewAsync_completed_metadata_only_job_is_not_requeued()
    {
        StoredImage();
        _capabilities.MediaPreviews = false;
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile("audio/mpeg"));
        _storage.CategorizeFile("audio/mpeg").Returns(FileCategory.Audio);
        var job = new JobBuilder().WithStatus(JobStatus.Ready).WithUser(_userId).Build();
        _previewJobs.FirstOrDefaultAsync(Arg.Any<Expression<Func<PreviewJob, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new PreviewJobBuilder().WithJob(job).WithVersion(_versionId).WithUser(_userId).Build());

        await _sut.GeneratePreviewAsync(_versionId, _userId, ct: TestContext.Current.CancellationToken);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!);
        await _jobs.DidNotReceiveWithAnyArgs().UpdateStatusAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task cached_preview_skips_dispatch_entirely()
    {
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _artifacts.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
            .Returns("https://cdn/preview");

        await _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview,
            TestContext.Current.CancellationToken);

        await _jobs.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task broker_unavailable_skips_dispatch_without_job_rows()
    {
        StoredImage();
        _broker.IsAvailable.Returns(false);

        await _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview,
            TestContext.Current.CancellationToken);

        await _jobs.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await _previewJobs.DidNotReceive().AddAsync(Arg.Any<PreviewJob>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task broker_recovery_dispatches_on_next_read()
    {
        StoredImage();
        _broker.IsAvailable.Returns(false);
        var ct = TestContext.Current.CancellationToken;

        await _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview, ct);

        _broker.IsAvailable.Returns(true);

        await _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview, ct);

        await _publisher.Received(1).PublishAsync(Arg.Any<byte[]>(), "image.png");
    }

    [Fact]
    public async Task active_job_skips_publish_without_new_job()
    {
        StoredImage();
        var job = new JobBuilder().WithStatus(JobStatus.Queued).WithUser(_userId).Build();
        _previewJobs.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<PreviewJob, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new PreviewJobBuilder().WithJob(job).WithVersion(_versionId).WithUser(_userId).Build());

        await _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview,
            TestContext.Current.CancellationToken);

        await _jobs.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task fresh_image_dispatch_creates_media_preview_job_and_publishes_its_id()
    {
        StoredImage();
        Job? created = null;
        _jobs.AddAsync(Arg.Do<Job>(j => created = j), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Job>());

        await _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview,
            TestContext.Current.CancellationToken);

        created.Should().NotBeNull();
        created!.Status.Should().Be(JobStatus.Queued);
        created.Type.Should().Be(JobType.MediaPreview);
        created.UserId.Should().Be(_userId);
        await _previewJobs.Received(1).AddAsync(
            Arg.Is<PreviewJob>(p =>
                p.JobId == created.Id && p.VersionId == _versionId
                                      && p.Kind == PreviewKind.Preview && p.UserId == _userId),
            Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == created.Id.ToString()), "image.png");
    }

    [Fact]
    public async Task fresh_document_dispatch_uses_document_preview_type()
    {
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _artifacts.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile("application/pdf"));
        _storage.CategorizeFile("application/pdf").Returns(FileCategory.Document);
        Job? created = null;
        _jobs.AddAsync(Arg.Do<Job>(j => created = j), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Job>());

        await _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview,
            TestContext.Current.CancellationToken);

        created.Should().NotBeNull();
        created!.Type.Should().Be(JobType.DocumentPreview);
        await _publisher.Received(1).PublishAsync(
            Arg.Any<byte[]>(), "document.pdf");
    }

    [Fact]
    public async Task publish_failure_marks_job_failed_and_throws()
    {
        StoredImage();
        Job? created = null;
        _jobs.AddAsync(Arg.Do<Job>(j => created = j), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Job>());
        _publisher.PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("broker down")));

        var act = () => _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        created.Should().NotBeNull();
        await _jobs.Received(1).UpdateStatusAsync(
            created!.Id, JobStatus.Failed, Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task terminal_job_requeues_same_id_without_new_job()
    {
        StoredImage();
        var job = new JobBuilder().WithStatus(JobStatus.Failed).WithUser(_userId).Build();
        _previewJobs.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<PreviewJob, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new PreviewJobBuilder().WithJob(job).WithVersion(_versionId).WithUser(_userId).Build());

        await _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview,
            TestContext.Current.CancellationToken);

        await _previewJobs.DidNotReceive().AddAsync(Arg.Any<PreviewJob>(), Arg.Any<CancellationToken>());
        await _jobs.Received(1).UpdateStatusAsync(
            job.Id, JobStatus.Queued, Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _jobs.Received(1).ClearErrorAsync(job.Id, Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(
            Arg.Is<byte[]>(b => Encoding.UTF8.GetString(b) == job.Id.ToString()), "image.png");
    }

    [Fact]
    public async Task getPreviewUrl_text_returns_structured_textPreview()
    {
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _versions.IsPromotedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(true);
        _artifacts.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile("application/json"));
        _storage.CategorizeFile("application/json").Returns(FileCategory.Text);
        _storage.DownloadFile(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("{\"hello\":\"world\"}")));

        var textPreview = new TextPreviewDto(
            [new TextPreviewLineDto("{\"hello\":\"world\"}", [])],
            "json",
            false,
            TextPreviewTruncation.None,
            false,
            17,
            null,
            "utf-8",
            1,
            0,
            1);

        _textPreview.GenerateHighlightedPreviewAsync(
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<long?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(textPreview);

        var result = await _sut.GetPreviewUrlAsync(_versionId, _userId, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.PreviewUrl.Should().BeNull();
        result.TextPreview.Should().Be(textPreview);
        result.ArchivePreview.Should().BeNull();
    }

    [Fact]
    public async Task getPreviewUrl_text_passes_the_version_size_as_totalBytes()
    {
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _versions.IsPromotedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(true);
        _versions.GetByIdAsync(_versionId, Arg.Any<CancellationToken>())
            .Returns(new FileVersion { Size = 4_096, VersionNumber = 1, MimeType = "text/plain" });
        _artifacts.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile("text/plain"));
        _storage.CategorizeFile("text/plain").Returns(FileCategory.Text);
        _storage.DownloadFile(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("hello\n")));

        await _sut.GetPreviewUrlAsync(_versionId, _userId, TestContext.Current.CancellationToken);

        await _textPreview.Received(1).GenerateHighlightedPreviewAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            4_096L,
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task getPreviewUrl_archive_returns_archivePreview_slot()
    {
        var archiveJson = "{\"FileCount\":1,\"FileName\":\"a.zip\",\"Entries\":[]}";
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _versions.IsPromotedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(true);
        _artifacts.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile("application/zip"));
        _storage.CategorizeFile("application/zip").Returns(FileCategory.Archive);
        _storage.DownloadSeekableFile(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream([1, 2, 3]));
        _archivePreview.GenerateArchivePreviewAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((archiveJson, "application/json"));

        var result = await _sut.GetPreviewUrlAsync(_versionId, _userId, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.PreviewUrl.Should().BeNull();
        result.TextPreview.Should().BeNull();
        result.ArchivePreview.Should().Be(archiveJson);
    }
}
