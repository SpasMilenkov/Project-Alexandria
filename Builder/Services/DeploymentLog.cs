using System.Diagnostics;
using System.Text;
using Builder.Workflow;

namespace Builder.Services;

public sealed class DeploymentLog : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private readonly SecretRedactor _redactor;
    private readonly TimeProvider _timeProvider;

    public string FilePath { get; }

    public DeploymentLog(InstallationContext context, TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _redactor = new SecretRedactor(() => context.Config.EnvironmentValues);
        var directory = Path.Combine(context.InstallPath, "installer-logs");
        var error = PathResolver.ValidateTarget(directory, probeWritable: false);

        if (error is not null) throw new IOException(error);

        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, $"install-{_timeProvider.GetUtcNow():yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.Read };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        _writer = new StreamWriter(new FileStream(FilePath, options), new UTF8Encoding(false)) { AutoFlush = true };
        context.DeploymentLogPath = FilePath;
        Write("session", "Source: " + context.SourceRoot);
        Write("session", "Target: " + context.InstallPath);
        Write("session", "Project: " + context.Config.ProjectName);
        Write("session", context.IsRepair ? "Repair; existing services and data are preserved." : "Fresh installation.");
    }

    public void Write(string category, string message)
    {
        var safe = _redactor.Redact(message);

        lock (_gate)
        {
            foreach (var line in safe.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
            {
                _writer.WriteLine($"{_timeProvider.GetUtcNow():O} [{category}] {line}");
            }
        }
    }

    public void RecordCommand(ProcessStartInfo command, ProcessResult? result)
    {
        var arguments = command.ArgumentList.Count > 0
            ? string.Join(" ", command.ArgumentList.Select(Quote))
            : command.Arguments;
        var description = Quote(command.FileName) + " " + arguments + " (in " + command.WorkingDirectory + ")";

        if (result is null)
        {
            Write("command", description);
        }
        else
        {
            Write("exit", $"{description}; exit={result.ExitCode}; timeout={result.TimedOut}");
        }
    }

    public IReadOnlyList<string> ReadRecent(int maximumLines = 2000)
    {
        lock (_gate)
        {
            _writer.Flush();

            return File.ReadLines(FilePath).TakeLast(Math.Clamp(maximumLines, 1, 10000)).ToArray();
        }
    }

    public void Dispose()
    {
        lock (_gate) _writer.Dispose();
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
}
