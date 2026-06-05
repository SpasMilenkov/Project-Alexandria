using Alexandria.Common.Exceptions.Transpilation;
using Alexandria.Data.Models.Enumerators;
using Alexandria.Dto.Files.Streaming;

namespace Alexandria.Common.Services;

public interface IVideoTranspilationService
{
    /// <summary>
    /// Runs two sequential ffmpeg passes against a video source file, producing
    /// HLS output in <c>{outputDirectory}/hls/</c> and DASH output in
    /// <c>{outputDirectory}/dash/</c>.
    /// </summary>
    /// <param name="jobId">Id of the job associated with this run.</param>
    /// <param name="inputPath">Absolute path to the source video file.</param>
    /// <param name="outputDirectory">
    /// Absolute path of the directory that will receive the <c>hls/</c> and
    /// <c>dash/</c> subdirectories. Must already exist.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="TranspilationOutput"/> describing the produced directories.
    /// </returns>
    /// <exception cref="TranspilationFfmpegException">
    /// Thrown when either ffmpeg pass exits with a non-zero code or fails to
    /// produce the expected output file.
    /// </exception>
    Task<TranspilationOutput> TranspileAsync(
        Guid jobId,
        string inputPath,
        string outputDirectory,
        VideoRung[] videoRungs,
        CancellationToken ct = default);
}