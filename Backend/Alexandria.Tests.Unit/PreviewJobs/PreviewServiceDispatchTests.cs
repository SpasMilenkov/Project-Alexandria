using System.Linq.Expressions;
using System.Text;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview;
using Alexandria.Tests.Common.Builders;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using File = Alexandria.Data.Models.File;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class PreviewServiceDispatchTests
{
    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly IPublisherService _publisher = Substitute.For<IPublisherService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();
    private readonly IPreviewJobRepository _previewJobs = Substitute.For<IPreviewJobRepository>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly ITextPreviewService _textPreview = Substitute.For<ITextPreviewService>();
    private readonly IArchivePreviewService _archivePreview = Substitute.For<IArchivePreviewService>();
    private readonly PreviewService _sut;

    private readonly Guid _versionId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public PreviewServiceDispatchTests()
    {
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.FileVersions.Returns(_versions);
        _unitOfWork.PreviewJobs.Returns(_previewJobs);
        _unitOfWork.Jobs.Returns(_jobs);
        _jobs.AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Job>());
        _previewJobs.AddAsync(Arg.Any<PreviewJob>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<PreviewJob>());

        var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        _sut = new PreviewService(
            _storage, _publisher, _unitOfWork,
            _textPreview, _archivePreview, cache);
    }

    private File StoredFile(string mimeType) =>
        new FileBuilder().WithMimeType(mimeType).WithOwner(_userId).Build();

    private void StoredImage()
    {
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _storage.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile("image/png"));
        _storage.CategorizeFile("image/png").Returns(FileCategory.Image);
    }

    [Fact]
    public async Task cached_preview_skips_dispatch_entirely()
    {
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _storage.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
            .Returns("https://cdn/preview");

        await _sut.GeneratePreviewAsync(_versionId, _userId, PreviewKind.Preview,
            TestContext.Current.CancellationToken);

        await _jobs.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<byte[]>(), Arg.Any<string>());
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
        _storage.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
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
    public async Task getPreviewUrl_text_returns_textPreview_slot()
    {
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _versions.IsPromotedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(true);
        _storage.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _files.FirstOrDefaultAsync(Arg.Any<Expression<Func<File, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(StoredFile("application/json"));
        _storage.CategorizeFile("application/json").Returns(FileCategory.Text);
        _storage.DownloadFile(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("{\"hello\":\"world\"}")));
        _textPreview.GenerateTextPreviewAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(("{\"hello\":\"world\"}", "application/json"));

        var result = await _sut.GetPreviewUrlAsync(_versionId, _userId, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.PreviewUrl.Should().BeNull();
        result.TextPreview.Should().Be("{\"hello\":\"world\"}");
        result.ArchivePreview.Should().BeNull();
    }

    [Fact]
    public async Task getPreviewUrl_archive_returns_archivePreview_slot()
    {
        var archiveJson = "{\"FileCount\":1,\"FileName\":\"a.zip\",\"Entries\":[]}";
        _files.VersionBelongsToUserAsync(_versionId, _userId, Arg.Any<CancellationToken>())
            .Returns("hash");
        _versions.IsEncryptedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(false);
        _versions.IsPromotedAsync(_versionId, Arg.Any<CancellationToken>()).Returns(true);
        _storage.GetCachedPreview(_versionId, PreviewKind.Preview, Arg.Any<CancellationToken>())
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