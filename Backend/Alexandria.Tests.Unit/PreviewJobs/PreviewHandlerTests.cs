using System.Linq.Expressions;
using Alexandria.Common;
using Alexandria.Common.Repositories;
using Alexandria.Common.Services;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files;
using Alexandria.Services.Preview.Documents;
using Alexandria.Services.Preview.Media;
using Alexandria.Services.Preview.Media.Dto;
using Alexandria.Tests.Common.Builders;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ImageHandler = Alexandria.Workers.Media.Handlers.ImagePreviewGenerationHandler;
using MediaHandler = Alexandria.Workers.Media.Handlers.MediaPreviewGenerationHandler;
using DocumentHandler = Alexandria.Workers.Document.Handlers.PreviewGenerationHandler;
using File = System.IO.File;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class PreviewHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPreviewJobRepository _previewJobs = Substitute.For<IPreviewJobRepository>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IFileRepository _files = Substitute.For<IFileRepository>();
    private readonly IFileVersionRepository _versions = Substitute.For<IFileVersionRepository>();

    private readonly Guid _jobId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public PreviewHandlerTests()
    {
        _unitOfWork.PreviewJobs.Returns(_previewJobs);
        _unitOfWork.Jobs.Returns(_jobs);
        _unitOfWork.Files.Returns(_files);
        _unitOfWork.FileVersions.Returns(_versions);
        _jobs.TryClaimJobAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private PreviewJob StoredJob(JobStatus status = JobStatus.Queued)
    {
        var job = new JobBuilder().WithId(_jobId).WithStatus(status).WithUser(_userId).Build();
        var previewJob = new PreviewJobBuilder().WithJob(job).WithVersion(_versionId).WithUser(_userId).Build();
        _previewJobs.GetByJobIdAsync(_jobId, Arg.Any<CancellationToken>()).Returns(previewJob);
        return previewJob;
    }

    private void StoredVersion(string mimeType = "image/png")
    {
        _versions.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<FileVersion, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new FileVersionBuilder().WithId(_versionId).WithContentHash(new byte[16]).Build());
        _files.GetMimeTypeByVersionIdAsync(_versionId, Arg.Any<CancellationToken>()).Returns(mimeType);
    }

    [Fact]
    public async Task malformed_message_throws_before_touching_repositories()
    {
        var sut = new ImageHandler(
            Substitute.For<ILogger<ImageHandler>>(),
            Substitute.For<IStorageService>(), Substitute.For<IImagePreviewService>(), _unitOfWork);

        var act = () => sut.HandleAsync("not-a-guid", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _previewJobs.DidNotReceive().GetByJobIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task unknown_job_id_throws_for_dead_letter()
    {
        _previewJobs.GetByJobIdAsync(_jobId, Arg.Any<CancellationToken>()).Returns((PreviewJob?)null);
        var sut = new ImageHandler(
            Substitute.For<ILogger<ImageHandler>>(),
            Substitute.For<IStorageService>(), Substitute.For<IImagePreviewService>(), _unitOfWork);

        var act = () => sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _jobs.DidNotReceive().TryClaimJobAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task duplicate_delivery_returns_silently_without_work()
    {
        StoredJob();
        _jobs.TryClaimJobAsync(_jobId, Arg.Any<CancellationToken>()).Returns(false);
        var storage = Substitute.For<IStorageService>();
        var sut = new ImageHandler(
            Substitute.For<ILogger<ImageHandler>>(),
            storage, Substitute.For<IImagePreviewService>(), _unitOfWork);

        await sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

        await storage.DidNotReceive().DownloadSeekableFile(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _jobs.DidNotReceive().UpdateStatusAsync(
            Arg.Any<Guid>(), Arg.Any<JobStatus>(), Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task image_success_uploads_both_artifacts_and_completes_job()
    {
        StoredJob();
        StoredVersion();
        var storage = Substitute.For<IStorageService>();
        storage.DownloadSeekableFile(_versionId, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(new byte[] { 1, 2, 3 }));
        var images = Substitute.For<IImagePreviewService>();
        images.GenerateImagePreviewAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(new byte[] { 4 }));
        images.GenerateImageThumbnailAsync(Arg.Any<Stream>(), Arg.Any<string?>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(new byte[] { 5 }));
        var sut = new ImageHandler(
            Substitute.For<ILogger<ImageHandler>>(), storage, images, _unitOfWork);

        await sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

        await storage.Received(2).UploadPreview(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), _versionId,
            Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<PreviewKind>(), Arg.Any<CancellationToken>());
        await _jobs.Received(1).UpdateStatusAsync(
            _jobId, JobStatus.Ready, Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task image_failure_marks_job_failed_and_rethrows_for_dead_letter()
    {
        StoredJob();
        StoredVersion();
        var storage = Substitute.For<IStorageService>();
        storage.DownloadSeekableFile(_versionId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Stream>(new InvalidOperationException("storage down")));
        var sut = new ImageHandler(
            Substitute.For<ILogger<ImageHandler>>(),
            storage, Substitute.For<IImagePreviewService>(), _unitOfWork);

        var act = () => sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _jobs.Received(1).UpdateStatusAsync(
            _jobId, JobStatus.Failed, Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task media_success_completes_job()
    {
        StoredJob();
        StoredVersion("video/mp4");
        var previewPath = Path.GetTempFileName();
        var thumbnailPath = Path.GetTempFileName();
        try
        {
            var storage = Substitute.For<IStorageService>();
            var media = Substitute.For<IMediaPreviewService>();
            media.GeneratePreviewAsync(Arg.Any<string>(), Arg.Any<FileCategory>(), Arg.Any<CancellationToken>())
                .Returns(new MediaPreviewResult
                {
                    PreviewPath = previewPath,
                    ThumbnailPath = thumbnailPath,
                    Metadata = new MediaMetadataDto()
                });
            var sut = new MediaHandler(
                Substitute.For<ILogger<MediaHandler>>(), storage, media, _unitOfWork);

            await sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

            await storage.Received(1).UploadMediaData(
                Arg.Any<Stream>(), Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<long>(),
                Arg.Any<string>(), _versionId, Arg.Any<MediaMetadataDto>(), Arg.Any<CancellationToken>());
            await _jobs.Received(1).UpdateStatusAsync(
                _jobId, JobStatus.Ready, Arg.Any<int?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(previewPath);
            File.Delete(thumbnailPath);
        }
    }

    [Fact]
    public async Task media_generation_failure_marks_job_failed_and_rethrows()
    {
        StoredJob();
        StoredVersion("video/mp4");
        var storage = Substitute.For<IStorageService>();
        var media = Substitute.For<IMediaPreviewService>();
        media.GeneratePreviewAsync(Arg.Any<string>(), Arg.Any<FileCategory>(), Arg.Any<CancellationToken>())
            .Returns(new MediaPreviewResult());
        var sut = new MediaHandler(
            Substitute.For<ILogger<MediaHandler>>(), storage, media, _unitOfWork);

        var act = () => sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _jobs.Received(1).UpdateStatusAsync(
            _jobId, JobStatus.Failed, Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task media_duplicate_delivery_returns_silently_without_work()
    {
        StoredJob();
        _jobs.TryClaimJobAsync(_jobId, Arg.Any<CancellationToken>()).Returns(false);
        var media = Substitute.For<IMediaPreviewService>();
        var sut = new MediaHandler(
            Substitute.For<ILogger<MediaHandler>>(),
            Substitute.For<IStorageService>(), media, _unitOfWork);

        await sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

        await media.DidNotReceive().GeneratePreviewAsync(
            Arg.Any<string>(), Arg.Any<FileCategory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task document_success_completes_job()
    {
        StoredJob();
        StoredVersion("application/pdf");
        var previewPath = Path.GetTempFileName();
        var thumbnailPath = Path.GetTempFileName();
        try
        {
            var storage = Substitute.For<IStorageService>();
            var pdf = Substitute.For<IPdfPreviewService>();
            pdf.GeneratePreviewAsync(Arg.Any<string>(), Arg.Any<FileCategory>(), Arg.Any<CancellationToken>())
                .Returns(new PdfPreviewResult(previewPath, thumbnailPath));
            var sut = new DocumentHandler(
                Substitute.For<ILogger<DocumentHandler>>(), storage, pdf, _unitOfWork);

            await sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

            await storage.Received(2).UploadPreview(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), _versionId,
                Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<PreviewKind>(), Arg.Any<CancellationToken>());
            await _jobs.Received(1).UpdateStatusAsync(
                _jobId, JobStatus.Ready, Arg.Any<int?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(previewPath);
            File.Delete(thumbnailPath);
        }
    }

    [Fact]
    public async Task document_missing_output_marks_job_failed_and_rethrows()
    {
        StoredJob();
        StoredVersion("application/pdf");
        var storage = Substitute.For<IStorageService>();
        var pdf = Substitute.For<IPdfPreviewService>();
        pdf.GeneratePreviewAsync(Arg.Any<string>(), Arg.Any<FileCategory>(), Arg.Any<CancellationToken>())
            .Returns(new PdfPreviewResult("/nonexistent/preview.pdf", "/nonexistent/thumb.jpg"));
        var sut = new DocumentHandler(
            Substitute.For<ILogger<DocumentHandler>>(), storage, pdf, _unitOfWork);

        var act = () => sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _jobs.Received(1).UpdateStatusAsync(
            _jobId, JobStatus.Failed, Arg.Any<int?>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task document_duplicate_delivery_returns_silently_without_work()
    {
        StoredJob();
        _jobs.TryClaimJobAsync(_jobId, Arg.Any<CancellationToken>()).Returns(false);
        var pdf = Substitute.For<IPdfPreviewService>();
        var sut = new DocumentHandler(
            Substitute.For<ILogger<DocumentHandler>>(), Substitute.For<IStorageService>(),
            pdf, _unitOfWork);

        await sut.HandleAsync(_jobId.ToString(), TestContext.Current.CancellationToken);

        await pdf.DidNotReceive().GeneratePreviewAsync(
            Arg.Any<string>(), Arg.Any<FileCategory>(), Arg.Any<CancellationToken>());
    }
}