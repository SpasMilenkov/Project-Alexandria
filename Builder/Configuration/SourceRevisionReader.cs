using System.Text.RegularExpressions;

namespace Builder.Configuration;

// Reads the checked-out commit straight from git's files, so recording it
// needs no git installation. Linked worktrees keep their branch refs in the
// shared repository, and git may have moved a ref into packed-refs, so both
// places are searched before giving up.
public static partial class SourceRevisionReader
{
    public const string Unknown = "unknown";

    private const string SymbolicPrefix = "ref: ";
    private const string GitDirectoryPrefix = "gitdir: ";

    public static string Read(string sourceRoot)
    {
        try
        {
            if (FindGitDirectory(sourceRoot) is not { } gitDirectory)
            {
                return Unknown;
            }

            var headPath = Path.Combine(gitDirectory, "HEAD");

            if (!File.Exists(headPath))
            {
                return Unknown;
            }

            var head = File.ReadAllText(headPath).Trim();

            if (!head.StartsWith(SymbolicPrefix, StringComparison.Ordinal))
            {
                return Validated(head);
            }

            var reference = head[SymbolicPrefix.Length..];
            var commonDirectory = CommonDirectory(gitDirectory);

            return Validated(LooseReference(gitDirectory, reference)
                             ?? LooseReference(commonDirectory, reference)
                             ?? PackedReference(commonDirectory, reference));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // An unreadable checkout must not block setup; the revision is
            // recorded as unknown and later runs treat it that way
            return Unknown;
        }
    }

    private static string? FindGitDirectory(string sourceRoot)
    {
        var dotGit = Path.Combine(sourceRoot, ".git");

        if (Directory.Exists(dotGit))
        {
            return dotGit;
        }

        if (!File.Exists(dotGit))
        {
            return null;
        }

        var link = File.ReadAllText(dotGit).Trim();

        if (!link.StartsWith(GitDirectoryPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return Path.GetFullPath(link[GitDirectoryPrefix.Length..], sourceRoot);
    }

    private static string CommonDirectory(string gitDirectory)
    {
        var pointer = Path.Combine(gitDirectory, "commondir");

        if (!File.Exists(pointer))
        {
            return gitDirectory;
        }

        return Path.GetFullPath(File.ReadAllText(pointer).Trim(), gitDirectory);
    }

    private static string? LooseReference(string directory, string reference)
    {
        var path = Path.Combine(directory, reference);

        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static string? PackedReference(string directory, string reference)
    {
        var path = Path.Combine(directory, "packed-refs");

        if (!File.Exists(path))
        {
            return null;
        }

        // Lines are "<commit> <ref>"; comments and peeled tag lines are skipped
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith('#') || line.StartsWith('^'))
            {
                continue;
            }

            var separator = line.IndexOf(' ');

            if (separator > 0 && line[(separator + 1)..] == reference)
            {
                return line[..separator];
            }
        }

        return null;
    }

    private static string Validated(string? revision)
    {
        if (revision is null || !CommitPattern().IsMatch(revision))
        {
            return Unknown;
        }

        return revision.ToLowerInvariant();
    }

    [GeneratedRegex("^[0-9a-fA-F]{40,64}$")]
    private static partial Regex CommitPattern();
}
