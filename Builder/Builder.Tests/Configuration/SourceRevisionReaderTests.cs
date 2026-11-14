using AwesomeAssertions;
using Builder.Configuration;

namespace Builder.Tests.Configuration;

public sealed class SourceRevisionReaderTests : IDisposable
{
    private const string Commit = "65e9b0ef1d2c3b4a5f60718293a4b5c6d7e8f901";
    private const string OtherCommit = "0123456789abcdef0123456789abcdef01234567";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "builder-revision-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        return path;
    }

    [Fact]
    public void Read_branchInLooseRef_returnsItsCommit()
    {
        Write("source/.git/HEAD", "ref: refs/heads/main\n");
        Write("source/.git/refs/heads/main", Commit + "\n");

        SourceRevisionReader.Read(Path.Combine(_root, "source")).Should().Be(Commit);
    }

    [Fact]
    public void Read_branchOnlyInPackedRefs_returnsItsCommit()
    {
        Write("source/.git/HEAD", "ref: refs/heads/main\n");
        Write("source/.git/packed-refs",
            "# pack-refs with: peeled fully-peeled sorted\n" +
            OtherCommit + " refs/heads/feature\n" +
            Commit + " refs/heads/main\n" +
            "^" + OtherCommit + "\n");

        SourceRevisionReader.Read(Path.Combine(_root, "source")).Should().Be(Commit);
    }

    [Fact]
    public void Read_linkedWorktree_readsTheBranchFromTheSharedRepository()
    {
        var worktreeGit = Path.Combine(_root, "main", ".git", "worktrees", "feature");

        Write("main/.git/worktrees/feature/HEAD", "ref: refs/heads/feature\n");
        Write("main/.git/worktrees/feature/commondir", "../..\n");
        Write("main/.git/refs/heads/feature", Commit + "\n");
        Write("feature/.git", "gitdir: " + worktreeGit + "\n");

        SourceRevisionReader.Read(Path.Combine(_root, "feature")).Should().Be(Commit);
    }

    [Fact]
    public void Read_detachedHead_returnsTheCommitInHead()
    {
        Write("source/.git/HEAD", Commit.ToUpperInvariant() + "\n");

        SourceRevisionReader.Read(Path.Combine(_root, "source")).Should().Be(Commit);
    }

    [Theory]
    [InlineData("ref: refs/heads/missing\n")]
    [InlineData("not a commit\n")]
    public void Read_unresolvableHead_isUnknown(string head)
    {
        Write("source/.git/HEAD", head);

        SourceRevisionReader.Read(Path.Combine(_root, "source")).Should().Be(SourceRevisionReader.Unknown);
    }

    [Fact]
    public void Read_folderWithoutGit_isUnknown()
    {
        Directory.CreateDirectory(Path.Combine(_root, "export"));

        SourceRevisionReader.Read(Path.Combine(_root, "export")).Should().Be(SourceRevisionReader.Unknown);
    }
}
