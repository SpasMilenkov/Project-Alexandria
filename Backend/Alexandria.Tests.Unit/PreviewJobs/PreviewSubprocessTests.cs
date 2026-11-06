using Alexandria.Common.Processes;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Services.Preview.Documents;
using Alexandria.Services.Preview.Media;
using Alexandria.Tests.Unit.Common;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.PreviewJobs;

public class PreviewSubprocessTests
{
    // Fails a hang as a test failure instead of stalling the whole run.
    private static readonly TimeSpan HangLimit = TimeSpan.FromSeconds(60);

    private static readonly CancellationToken Cancelled = new(canceled: true);

    private static bool OnPath(string binary) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator)
        .Any(dir => File.Exists(Path.Combine(dir, binary)));

    private static async Task RunToolAsync(string fileName, string arguments)
    {
        var result = await ExternalProcess.RunAsync(
            fileName,
            arguments,
            HangLimit,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(0, result.StdErrTail);
    }

    private static void DeleteIfPresent(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task PdfGeneratePreviewAsync_cancelled_doesNotStartFallback()
    {
        var logger = new RecordingLogger<PdfPreviewService>();
        var sut = new PdfPreviewService(logger);

        var act = () => sut.GeneratePreviewAsync("/nonexistent/input.pdf", FileCategory.Pdf, Cancelled);

        await act.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().NotContain(e => e.Message.Contains("falling back"));
    }

    [Fact]
    public async Task PdfGenerateThumbnailAsync_cancelled_doesNotStartFallback()
    {
        var logger = new RecordingLogger<PdfPreviewService>();
        var sut = new PdfPreviewService(logger);

        var act = () => sut.GenerateThumbnailAsync("/nonexistent/input.pdf", Cancelled);

        await act.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().NotContain(e => e.Message.Contains("falling back"));
    }

    [Fact]
    public async Task MediaTryExtractAlbumArtworkAsync_cancelled_throwsInsteadOfReportingNoArtwork()
    {
        var logger = new RecordingLogger<MediaPreviewService>();
        var sut = new MediaPreviewService(logger);

        var act = () => sut.TryExtractAlbumArtworkAsync("/nonexistent/input.flac", "/nonexistent/output.jpg", Cancelled);

        await act.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().NotContain(e => e.Message.Contains("falling back"));
    }

    [Fact]
    public async Task MediaGeneratePreviewAsync_cancelled_doesNotLogAFailure()
    {
        var logger = new RecordingLogger<MediaPreviewService>();
        var sut = new MediaPreviewService(logger);

        var act = () => sut.GeneratePreviewAsync("/nonexistent/input.mp4", FileCategory.Video, Cancelled);

        await act.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task MediaGeneratePreviewAsync_audioWithTagLargerThanPipe_completes()
    {
        Assert.SkipUnless(OnPath("ffmpeg") && OnPath("ffprobe"), "ffmpeg is not installed.");

        var baseName = $"preview-subprocess-test-{Guid.NewGuid()}";
        var temp = Path.GetTempPath();
        var metadataPath = Path.Combine(temp, $"{baseName}.txt");
        var inputPath = Path.Combine(temp, $"{baseName}.flac");
        var thumbnailPath = Path.Combine(temp, $"{baseName}_thumb.jpg");
        var previewPath = Path.Combine(temp, $"{baseName}_preview.mp3");

        try
        {
            // ffmpeg echoes every tag to stderr when it opens a file, so one long tag is enough
            // to overflow an unread 64 KiB pipe before any encoding starts.
            var lyrics = string.Join("\\\n", Enumerable.Range(0, 4000).Select(i => $"embedded lyric line number {i}"));
            await File.WriteAllTextAsync(metadataPath, $";FFMETADATA1\nlyrics={lyrics}\n",
                TestContext.Current.CancellationToken);

            await RunToolAsync(
                "ffmpeg",
                $"-v error -f lavfi -i sine=frequency=440:duration=2 -i \"{metadataPath}\" " +
                $"-map 0 -map_metadata 1 -c:a flac \"{inputPath}\"");

            var sut = new MediaPreviewService(NullLogger<MediaPreviewService>.Instance);

            var result = await sut
                .GeneratePreviewAsync(inputPath, FileCategory.Audio, TestContext.Current.CancellationToken)
                .WaitAsync(HangLimit, TestContext.Current.CancellationToken);

            result.Should().NotBeNull();
            result.Metadata!.HasAudio.Should().BeTrue();
            new FileInfo(thumbnailPath).Length.Should().BeGreaterThan(0);
            new FileInfo(previewPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            DeleteIfPresent(metadataPath, inputPath, thumbnailPath, previewPath);
        }
    }

    [Fact]
    public async Task MediaGeneratePreviewAsync_healthyVideo_producesThumbnailAndClip()
    {
        Assert.SkipUnless(OnPath("ffmpeg") && OnPath("ffprobe"), "ffmpeg is not installed.");

        var baseName = $"preview-subprocess-test-{Guid.NewGuid()}";
        var temp = Path.GetTempPath();
        var inputPath = Path.Combine(temp, $"{baseName}.mp4");
        var thumbnailPath = Path.Combine(temp, $"{baseName}_thumb.jpg");
        var previewPath = Path.Combine(temp, $"{baseName}_preview.mp4");

        try
        {
            await RunToolAsync(
                "ffmpeg",
                $"-v error -f lavfi -i testsrc2=size=160x90:rate=10:duration=2 -f lavfi -i sine=duration=2 " +
                $"-c:v libx264 -preset ultrafast -threads 1 -c:a aac \"{inputPath}\"");

            var sut = new MediaPreviewService(NullLogger<MediaPreviewService>.Instance);

            var result = await sut
                .GeneratePreviewAsync(inputPath, FileCategory.Video, TestContext.Current.CancellationToken)
                .WaitAsync(HangLimit, TestContext.Current.CancellationToken);

            result.Should().NotBeNull();
            result.Metadata!.HasVideo.Should().BeTrue();
            new FileInfo(thumbnailPath).Length.Should().BeGreaterThan(0);
            new FileInfo(previewPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            DeleteIfPresent(inputPath, thumbnailPath, previewPath);
        }
    }

    [Theory]
    [InlineData(FileCategory.Video, "input.mp4",
        "-f lavfi -i testsrc2=size=160x90:rate=10:duration=1 -f lavfi -i sine=duration=1 " +
        "-c:v libx264 -preset ultrafast -threads 1 -c:a aac")]
    [InlineData(FileCategory.Audio, "input.flac", "-f lavfi -i sine=frequency=440:duration=1 -c:a flac")]
    public async Task MediaGeneratePreviewAsync_writesOutputsNextToTheInput(
        FileCategory category,
        string inputName,
        string encodeArguments)
    {
        Assert.SkipUnless(OnPath("ffmpeg") && OnPath("ffprobe"), "ffmpeg is not installed.");

        var directory = Directory.CreateTempSubdirectory("preview-subprocess-test-").FullName;
        var inputPath = Path.Combine(directory, inputName);

        try
        {
            await RunToolAsync("ffmpeg", $"-v error {encodeArguments} \"{inputPath}\"");

            var sut = new MediaPreviewService(NullLogger<MediaPreviewService>.Instance);

            var result = await sut
                .GeneratePreviewAsync(inputPath, category, TestContext.Current.CancellationToken)
                .WaitAsync(HangLimit, TestContext.Current.CancellationToken);

            Path.GetDirectoryName(result!.PreviewPath).Should().Be(directory);
            Path.GetDirectoryName(result.ThumbnailPath).Should().Be(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PdfGeneratePreviewAsync_healthyPdf_producesPreviewAndThumbnail()
    {
        Assert.SkipUnless(OnPath("gs") && OnPath("pdftocairo"), "Ghostscript or poppler is not installed.");

        var directory = Directory.CreateTempSubdirectory("preview-subprocess-test-").FullName;
        var inputPath = Path.Combine(directory, "input.pdf");

        try
        {
            await RunToolAsync("gs", $"-q -dNOPAUSE -dBATCH -sDEVICE=pdfwrite -sOutputFile=\"{inputPath}\" -c showpage");

            var sut = new PdfPreviewService(NullLogger<PdfPreviewService>.Instance);

            var result = await sut
                .GeneratePreviewAsync(inputPath, FileCategory.Pdf, TestContext.Current.CancellationToken)
                .WaitAsync(HangLimit, TestContext.Current.CancellationToken);

            new FileInfo(result.PreviewPath).Length.Should().BeGreaterThan(0);
            new FileInfo(result.ThumbnailPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
