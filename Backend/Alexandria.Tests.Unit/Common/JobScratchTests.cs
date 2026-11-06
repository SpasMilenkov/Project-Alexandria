using Alexandria.Common.Processes;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alexandria.Tests.Unit.Common;

public sealed class JobScratchTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("job-scratch-test-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Create_makesASeparateFolderForEachJob()
    {
        var first = JobScratch.Create(_root);
        var second = JobScratch.Create(_root);

        Directory.Exists(first).Should().BeTrue();
        Directory.Exists(second).Should().BeTrue();
        first.Should().NotBe(second);
        Path.GetDirectoryName(first).Should().Be(_root);
    }

    [Fact]
    public void Delete_removesFilesNobodyTrackedByName()
    {
        var folder = JobScratch.Create(_root);
        File.WriteAllText(Path.Combine(folder, ".~lock.input.pdf#"), "lock");
        var toolFolder = Directory.CreateDirectory(Path.Combine(folder, "lu123.tmp")).FullName;
        File.WriteAllText(Path.Combine(toolFolder, "lu124.tmp"), "working file");

        JobScratch.Delete(folder, NullLogger.Instance);

        Directory.Exists(folder).Should().BeFalse();
    }

    [Fact]
    public void Delete_folderAlreadyGone_doesNothing()
    {
        var act = () => JobScratch.Delete(Path.Combine(_root, "job-gone"), NullLogger.Instance);

        act.Should().NotThrow();
    }

    [Fact]
    public void Delete_failure_logsInsteadOfThrowing()
    {
        Assert.SkipWhen(
            !OperatingSystem.IsLinux() || Environment.UserName == "root",
            "Needs a user that file permissions apply to.");

        var logger = new RecordingLogger<JobScratchTests>();
        var folder = JobScratch.Create(_root);
        var locked = Directory.CreateDirectory(Path.Combine(folder, "locked")).FullName;
        File.WriteAllText(Path.Combine(locked, "output"), "partial");
        File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            JobScratch.Delete(folder, logger);

            logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void SweepLeftovers_removesJobFoldersAndLeavesEverythingElse()
    {
        var logger = new RecordingLogger<JobScratchTests>();
        var leftover = JobScratch.Create(_root);
        File.WriteAllText(Path.Combine(leftover, "input.mp4"), "left behind by a killed worker");
        var other = Directory.CreateDirectory(Path.Combine(_root, "lu123.tmp")).FullName;
        var fileWithSamePrefix = Path.Combine(_root, "job-notes.txt");
        File.WriteAllText(fileWithSamePrefix, "not a job folder");

        JobScratch.SweepLeftovers(_root, logger);

        Directory.Exists(leftover).Should().BeFalse();
        Directory.Exists(other).Should().BeTrue();
        File.Exists(fileWithSamePrefix).Should().BeTrue();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information);
    }

    [Fact]
    public void SweepLeftovers_nothingToDo_staysQuiet()
    {
        var logger = new RecordingLogger<JobScratchTests>();

        JobScratch.SweepLeftovers(_root, logger);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void SweepLeftovers_rootMissing_logsInsteadOfStoppingTheWorker()
    {
        var logger = new RecordingLogger<JobScratchTests>();

        var act = () => JobScratch.SweepLeftovers(Path.Combine(_root, "missing"), logger);

        act.Should().NotThrow();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
    }
}
