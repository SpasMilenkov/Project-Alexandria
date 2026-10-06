using System.Diagnostics;
using AwesomeAssertions;
using Builder.Services;
using Builder.Workflow;

namespace Builder.Tests.Services;

public class DeploymentLogTests : IDisposable
{
    private readonly string _target = Path.Combine(Path.GetTempPath(), "builder log tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Write_sensitiveOutputAndLaterStorageKeys_redactsBeforePersistence()
    {
        var context = CreateContext();
        context.Config.EnvironmentValues["ADMIN_PASSWORD"] = "Synthetic password /#";
        using var log = new DeploymentLog(context);
        log.Write("stage", "Building Alexandria");
        log.Write("output", "Password=unknown-password; amqp://user:broker-secret@rabbitmq; Bearer abc.def.ghi");
        log.Write("output", "Synthetic password /# " + Uri.EscapeDataString("Synthetic password /#"));
        context.Config.EnvironmentValues["GARAGE_S3_SECRET_KEY"] = "later-storage-secret";
        log.Write("error", "Storage refused later-storage-secret; exit 1");

        var contents = File.ReadAllText(log.FilePath);

        contents.Should().Contain("Building Alexandria").And.Contain("Storage refused").And.Contain("exit 1");
        foreach (var secret in new[] { "unknown-password", "broker-secret", "abc.def.ghi", "Synthetic password /#", "later-storage-secret" })
        {
            contents.Should().NotContain(secret);
        }
        contents.Should().Contain(context.SourceRoot).And.Contain(context.InstallPath).And.Contain(context.Config.ProjectName);
        Path.IsPathFullyQualified(context.DeploymentLogPath!).Should().BeTrue();
        contents.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().OnlyContain(line => line.Contains("+00:00 ["));

        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(log.FilePath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public void RecordCommand_sensitiveCapturedResult_logsOnlyCommandAndExitMetadata()
    {
        using var log = new DeploymentLog(CreateContext());
        var command = new ProcessStartInfo("docker") { WorkingDirectory = _target };
        foreach (var argument in new[] { "compose", "--project-name", "builder-log-test", "config" }) command.ArgumentList.Add(argument);
        command.Environment["UNKNOWN_PASSWORD"] = "environment-must-not-appear";
        log.RecordCommand(command, null);
        log.RecordCommand(command, new ProcessResult(3, "raw-resolved-compose-secret", "raw-command-secret", true));

        var contents = File.ReadAllText(log.FilePath);

        contents.Should().Contain("[command]").And.Contain("'compose' '--project-name' 'builder-log-test'");
        contents.Should().Contain("exit=3; timeout=True");
        contents.Should().NotContain("environment-must-not-appear").And.NotContain("raw-resolved-compose-secret").And.NotContain("raw-command-secret");
    }

    [Fact]
    public void ReadRecent_concurrentWriters_retainsWholeTimestampedLines()
    {
        using var log = new DeploymentLog(CreateContext());
        Parallel.For(0, 200, index => log.Write("output", $"row-{index}"));

        var lines = log.ReadRecent(200);

        lines.Should().HaveCount(200);
        Enumerable.Range(0, 200).Should().OnlyContain(index => lines.Count(line => line.EndsWith("row-" + index, StringComparison.Ordinal)) == 1);
    }

    [Fact]
    public void Constructor_symlinkLogDirectory_rejectsBeforeWritingOutsideTarget()
    {
        if (OperatingSystem.IsWindows()) return;

        Directory.CreateDirectory(_target);
        var outside = Path.Combine(_target, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(_target, "installer-logs"), outside);

        var action = () => new DeploymentLog(CreateContext());

        action.Should().Throw<IOException>().WithMessage("*symbolic link*");
        Directory.EnumerateFiles(outside).Should().BeEmpty();
    }

    private InstallationContext CreateContext() => new()
    {
        InstallPath = _target,
        SourceRoot = Path.Combine(_target, "source"),
        Config = new() { InstallPath = _target, SourceRoot = Path.Combine(_target, "source"), ProjectName = "builder-log-test" },
    };

    public void Dispose()
    {
        if (Directory.Exists(_target)) Directory.Delete(_target, recursive: true);
    }
}
