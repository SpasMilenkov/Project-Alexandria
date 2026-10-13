using AwesomeAssertions;
using Builder.Models;
using Builder.Services;
using Builder.Workflow;

namespace Builder.Tests.Services;

public class GarageCredentialPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "builder-garage-persistence", Guid.NewGuid().ToString("N"));
    private readonly InstallationContext _context;
    private string EnvPath => Path.Combine(_directory, ".env");
    private string HandoffPath => Path.Combine(_directory, "init-output", "garage-credentials.env");
    private string ReceiptPath => Path.Combine(_directory, "init-output", GarageCredentialPersistence.ReceiptFileName);

    public GarageCredentialPersistenceTests()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "init-output"));
        _context = new InstallationContext { InstallPath = _directory };
        _context.Config.GenerationId = "synthetic-generation";
        _context.Config.EnvironmentValues = GarageCredentialsFileParser.ToEnvValues(new(
            "placeholder", "placeholder", "placeholder", "placeholder", "placeholder", "placeholder"));
        _context.Config.EnvironmentValues["DB_PASSWORD"] = "existing-database-secret";
        _context.Config.EnvironmentValues["CUSTOM_SETTING"] = "unknown$literal = # retained";
        _context.Config.EnvironmentValues["ALEXANDRIA_GENERATION_ID"] = _context.Config.GenerationId;
        EnvGenerator.WriteFile(EnvPath, _context.Config.EnvironmentValues);
    }

    [Fact]
    public void Commit_completeMatchingHandoff_verifiesPersistenceBeforeDeletingHandoff()
    {
        WriteHandoff();

        GarageCredentialPersistence.Commit(_context, HandoffPath);

        File.Exists(HandoffPath).Should().BeFalse();
        File.ReadAllText(ReceiptPath).Should().Be("synthetic-generation\n");

        if (!OperatingSystem.IsWindows())
            File.GetUnixFileMode(ReceiptPath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var persisted = EnvGenerator.Parse(File.ReadAllText(EnvPath));
        persisted.Should().BeEquivalentTo(_context.Config.EnvironmentValues);
        persisted["GARAGE_S3_ACCESS_KEY"].Should().Be("GK_master");
        persisted["GARAGE_S3_WORKER_SECRET_KEY"].Should().Be("preview-secret");
        persisted["GARAGE_S3_TRANSPILATION_WORKER_SECRET_KEY"].Should().Be("streaming-secret");
        persisted["DB_PASSWORD"].Should().Be("existing-database-secret");
        persisted["CUSTOM_SETTING"].Should().Be("unknown$literal = # retained");
        _context.GarageCredentials.Should().NotBeNull();
    }

    [Fact]
    public void Commit_receiptPublicationFails_retainsHandoffAfterVerifiedEnvironmentWrite()
    {
        WriteHandoff();
        Directory.CreateDirectory(ReceiptPath);

        var action = () => GarageCredentialPersistence.Commit(_context, HandoffPath);

        action.Should().Throw<IOException>();
        File.Exists(HandoffPath).Should().BeTrue();
        EnvGenerator.Parse(File.ReadAllText(EnvPath))["GARAGE_S3_ACCESS_KEY"].Should().Be("GK_master");
        Directory.EnumerateFiles(Path.GetDirectoryName(ReceiptPath)!, ".garage-credentials.persisted.*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Commit_receiptSymlink_preservesTargetAndRecoveryHandoff()
    {
        if (OperatingSystem.IsWindows()) return;

        WriteHandoff();
        var existing = Path.Combine(_directory, "unrelated.txt");
        File.WriteAllText(existing, "preserve unrelated content");
        File.CreateSymbolicLink(ReceiptPath, existing);

        var action = () => GarageCredentialPersistence.Commit(_context, HandoffPath);

        action.Should().Throw<InvalidOperationException>().WithMessage("*regular file*");
        File.ReadAllText(existing).Should().Be("preserve unrelated content");
        File.Exists(HandoffPath).Should().BeTrue();
    }

    [Fact]
    public void Commit_environmentGenerationDisagrees_preservesEnvironmentAndHandoff()
    {
        WriteHandoff();
        _context.Config.EnvironmentValues["ALEXANDRIA_GENERATION_ID"] = "another-generation";
        EnvGenerator.WriteFile(EnvPath, _context.Config.EnvironmentValues);
        var original = File.ReadAllText(EnvPath);

        var action = () => GarageCredentialPersistence.Commit(_context, HandoffPath);

        action.Should().Throw<InvalidOperationException>().WithMessage("*another or an invalid generation*");
        File.ReadAllText(EnvPath).Should().Be(original);
        File.Exists(HandoffPath).Should().BeTrue();
        File.Exists(ReceiptPath).Should().BeFalse();
    }

    [Fact]
    public void AcknowledgePersistedKeys_existingCompleteEnvironment_recreatesOnlyNonsecretReceipt()
    {
        WriteHandoff();
        GarageCredentialPersistence.Commit(_context, HandoffPath);
        File.Delete(ReceiptPath);
        var original = File.ReadAllText(EnvPath);

        GarageCredentialPersistence.AcknowledgePersistedKeys(_context).Should().BeTrue();

        File.ReadAllText(ReceiptPath).Should().Be("synthetic-generation\n");
        File.ReadAllText(EnvPath).Should().Be(original);
        File.Exists(HandoffPath).Should().BeFalse();
    }

    [Fact]
    public void AcknowledgePersistedKeys_completeContextButPendingActualEnvironment_doesNotWriteReceipt()
    {
        foreach (var (key, value) in GarageCredentialsFileParser.ToEnvValues(new("GK1", "s1", "GK2", "s2", "GK3", "s3")))
            _context.Config.EnvironmentValues[key] = value;

        GarageCredentialPersistence.AcknowledgePersistedKeys(_context).Should().BeFalse();

        File.Exists(ReceiptPath).Should().BeFalse();
    }

    [Fact]
    public void AcknowledgePersistedKeys_completeEnvironmentWithStaleGeneration_rejectsReceipt()
    {
        WriteHandoff();
        GarageCredentialPersistence.Commit(_context, HandoffPath);
        File.Delete(ReceiptPath);
        var values = EnvGenerator.Parse(File.ReadAllText(EnvPath));
        values["ALEXANDRIA_GENERATION_ID"] = "another-generation";
        EnvGenerator.WriteFile(EnvPath, values);

        var action = () => GarageCredentialPersistence.AcknowledgePersistedKeys(_context);

        action.Should().Throw<InvalidOperationException>().WithMessage("*another or an invalid generation*");
        File.Exists(ReceiptPath).Should().BeFalse();
    }

    [Fact]
    public void AcknowledgePersistedKeys_actualKeysChangedAfterReview_doesNotWriteReceipt()
    {
        WriteHandoff();
        GarageCredentialPersistence.Commit(_context, HandoffPath);
        File.Delete(ReceiptPath);
        var values = EnvGenerator.Parse(File.ReadAllText(EnvPath));
        values["GARAGE_S3_SECRET_KEY"] = "changed-after-review";
        EnvGenerator.WriteFile(EnvPath, values);

        var action = () => GarageCredentialPersistence.AcknowledgePersistedKeys(_context);

        action.Should().Throw<InvalidOperationException>().WithMessage("*changed after review*");
        File.Exists(ReceiptPath).Should().BeFalse();
    }

    [Theory]
    [InlineData("wrong-generation", false)]
    [InlineData("synthetic-generation", true)]
    public void Commit_staleOrIncompleteHandoff_preservesBothRecoveryCopies(string generation, bool partial)
    {
        WriteHandoff(generation, partial);
        var original = File.ReadAllText(EnvPath);

        var action = () => GarageCredentialPersistence.Commit(_context, HandoffPath);

        action.Should().Throw<InvalidOperationException>();
        File.ReadAllText(EnvPath).Should().Be(original);
        File.Exists(HandoffPath).Should().BeTrue();
        _context.GarageCredentials.Should().BeNull();
    }

    [Fact]
    public void Commit_establishedKeyDisagrees_preservesBothCopiesAndEstablishedEnvironment()
    {
        _context.Config.EnvironmentValues["GARAGE_S3_ACCESS_KEY"] = "GK_established";
        EnvGenerator.WriteFile(EnvPath, _context.Config.EnvironmentValues);
        WriteHandoff();
        var original = File.ReadAllText(EnvPath);

        var action = () => GarageCredentialPersistence.Commit(_context, HandoffPath);

        action.Should().Throw<InvalidOperationException>().WithMessage("*disagrees*");
        File.ReadAllText(EnvPath).Should().Be(original);
        File.Exists(HandoffPath).Should().BeTrue();
    }

    [Fact]
    public void Commit_cancelledBeforePersistence_leavesArtifactsUntouched()
    {
        WriteHandoff();
        var original = File.ReadAllText(EnvPath);
        _context.Cancellation.Cancel();

        var action = () => GarageCredentialPersistence.Commit(_context, HandoffPath);

        action.Should().Throw<OperationCanceledException>();
        File.ReadAllText(EnvPath).Should().Be(original);
        File.Exists(HandoffPath).Should().BeTrue();
    }

    [Fact]
    public void Commit_invalidExistingEnvironment_keepsHandoffForRecovery()
    {
        WriteHandoff();
        File.WriteAllText(EnvPath, "DB_PASSWORD=${UNRESOLVED_PROCESS_VALUE}\n");

        var action = () => GarageCredentialPersistence.Commit(_context, HandoffPath);

        action.Should().Throw<FormatException>();
        File.Exists(HandoffPath).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("placeholder")]
    [InlineData("PLACEHOLDER")]
    public void HasPersistedKeys_onePendingSlot_rejectsIncompleteKeySet(string pending)
    {
        _context.Config.EnvironmentValues = GarageCredentialsFileParser.ToEnvValues(new(
            "GK1", "secret1", "GK2", "secret2", "GK3", pending));

        GarageCredentialPersistence.HasPersistedKeys(_context).Should().BeFalse();
    }

    [Fact]
    public void HasPersistedKeys_completeSet_acceptsSafeResumeWithoutHandoff()
    {
        _context.Config.EnvironmentValues = GarageCredentialsFileParser.ToEnvValues(new(
            "GK1", "secret1", "GK2", "secret2", "GK3", "secret3"));

        GarageCredentialPersistence.HasPersistedKeys(_context).Should().BeTrue();
    }

    private void WriteHandoff(string generation = "synthetic-generation", bool partial = false)
    {
        var values = new Dictionary<string, string>
        {
            ["ALEXANDRIA_GENERATION_ID"] = generation,
            ["GARAGE_S3_ACCESS_KEY"] = "GK_master",
            ["GARAGE_S3_SECRET_KEY"] = "master-secret",
            ["GARAGE_PREVIEW_KEY_ID"] = "GK_preview",
            ["GARAGE_PREVIEW_SECRET"] = "preview-secret",
            ["GARAGE_STREAMING_KEY_ID"] = "GK_streaming",
        };

        if (!partial)
        {
            values["GARAGE_STREAMING_SECRET"] = "streaming-secret";
        }

        EnvGenerator.WriteFile(HandoffPath, values);
    }

    public void Dispose()
    {
        _context.Cancellation.Dispose();
        Directory.Delete(_directory, recursive: true);
    }
}
