using System.Text;

namespace Builder.Services;

public static class ProtectedFile
{
    public static void WriteAtomic(string path, string content, UnixFileMode? mode = null)
    {
        WriteAtomic(path, content, mode, beforeReplace: null);
    }

    internal static void WriteAtomic(string path, string content, UnixFileMode? mode, Action<string>? beforeReplace)
    {
        var destination = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(destination)!;
        var staging = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        var encoding = new UTF8Encoding(false, true);
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            using (var stream = new FileStream(staging, options))
            {
                using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            beforeReplace?.Invoke(staging);

            if (File.ReadAllText(staging, encoding) != content)
            {
                throw new IOException("The staged file failed read-back validation.");
            }

            if (!OperatingSystem.IsWindows() && mode is not null)
            {
                File.SetUnixFileMode(staging, mode.Value);
            }

            File.Move(staging, destination, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(staging);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
