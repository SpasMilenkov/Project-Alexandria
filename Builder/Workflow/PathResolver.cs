namespace Builder.Workflow;

public static class PathResolver
{
    public static bool TryResolve(
        string input,
        string launchDirectory,
        out string resolvedPath,
        out string? error)
    {
        resolvedPath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Enter a folder path.";

            return false;
        }

        if (input.Any(char.IsControl))
        {
            error = "Folder paths cannot contain control characters.";

            return false;
        }

        var path = input.Trim();

        if (path[0] is '\'' or '"')
        {
            if (path.Length < 2 || path[^1] != path[0])
            {
                error = "Close the quote around the folder path.";

                return false;
            }

            path = path[1..^1];
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Enter a folder path.";

            return false;
        }

        if (path[0] == '~')
        {
            if (path.Length > 1 && path[1] != '/' &&
                (!OperatingSystem.IsWindows() || path[1] != '\\'))
            {
                error = "Use ~ or ~/folder for your home folder; ~otheruser is unsupported.";

                return false;
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (string.IsNullOrEmpty(home))
            {
                error = "Your home folder could not be found. Enter an absolute folder path.";

                return false;
            }

            path = path.Length == 1 ? home : Path.Join(home, path[2..]);
        }

        try
        {
            resolvedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path, launchDirectory));

            return true;
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            error = $"Invalid folder path: {exception.Message}";

            return false;
        }
    }

    public static bool PathsEqual(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            comparison);
    }

    public static string? ValidateTarget(string absolutePath, bool probeWritable = true)
    {
        if (!Path.IsPathFullyQualified(absolutePath))
        {
            return "The installation folder must be resolved to an absolute path first.";
        }

        var createdDirectories = new List<string>();
        string? probePath = null;
        string? error = null;

        try
        {
            error = ValidateDirectoryComponents(absolutePath);

            if (error is not null || !probeWritable)
            {
                return error;
            }

            var missing = new Stack<string>();
            var directory = absolutePath;

            while (!Directory.Exists(directory))
            {
                missing.Push(directory);
                directory = Path.GetDirectoryName(directory)!;
            }

            while (missing.TryPop(out directory))
            {
                error = ValidateDirectoryComponents(directory);

                if (error is not null)
                {
                    throw new IOException(error);
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    createdDirectories.Add(directory);
                }
            }

            error = ValidateDirectoryComponents(absolutePath);

            if (error is not null)
            {
                throw new IOException(error);
            }

            var candidate = Path.Combine(absolutePath, $".alexandria-write-probe-{Guid.NewGuid():N}");

            using (var probe = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                probePath = candidate;
                probe.WriteByte(0);
                probe.Flush();
            }
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            error = $"Cannot write to installation folder '{absolutePath}': {exception.Message}";
        }
        finally
        {
            if (probePath is not null)
            {
                try
                {
                    File.Delete(probePath);
                }
                catch (Exception exception) when (IsPathException(exception))
                {
                    error = $"Could not remove the write probe '{probePath}': {exception.Message}";
                }
            }

            for (var index = createdDirectories.Count - 1; index >= 0; index--)
            {
                try
                {
                    var createdDirectory = createdDirectories[index];

                    if (!Directory.Exists(createdDirectory))
                    {
                        continue;
                    }

                    var cleanupError = ValidateDirectoryComponents(createdDirectory);

                    if (cleanupError is not null)
                    {
                        error = cleanupError;

                        continue;
                    }

                    if (Directory.EnumerateFileSystemEntries(createdDirectory).Any())
                    {
                        continue;
                    }

                    // Non-recursive deletion preserves anything created by another process.
                    Directory.Delete(createdDirectory, recursive: false);
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch (Exception exception) when (IsPathException(exception))
                {
                    error = $"Could not remove the empty probe folder '{createdDirectories[index]}': {exception.Message}";
                }
            }
        }

        return error;
    }

    private static string? ValidateDirectoryComponents(string absolutePath)
    {
        var directory = new DirectoryInfo(absolutePath);
        var components = new Stack<string>();

        while (directory is not null)
        {
            components.Push(directory.FullName);
            directory = directory.Parent;
        }

        foreach (var component in components)
        {
            FileAttributes attributes;

            try
            {
                attributes = File.GetAttributes(component);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return $"Installation folder uses a symbolic link or junction at '{component}'. " +
                       "Choose the real folder path so an existing installation cannot be mistaken for a new one.";
            }

            if ((attributes & FileAttributes.Directory) == 0)
            {
                return $"'{component}' is a file. Choose a folder for installation settings.";
            }
        }

        return null;
    }

    private static bool IsPathException(Exception exception) =>
        exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException;
}
