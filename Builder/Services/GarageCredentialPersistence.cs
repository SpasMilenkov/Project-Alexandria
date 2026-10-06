using Builder.Workflow;

namespace Builder.Services;

public static class GarageCredentialPersistence
{
    public const string ReceiptFileName = "garage-credentials.persisted";

    public static bool HasPersistedKeys(InstallationContext context) =>
        HasCompleteKeys(context.Config.EnvironmentValues);

    private static bool HasCompleteKeys(IReadOnlyDictionary<string, string> values) =>
        GarageCredentialsFileParser.ToEnvValues(new("", "", "", "", "", "")).Keys.All(key =>
            values.TryGetValue(key, out var value) &&
            !IsPending(value));

    public static void Commit(InstallationContext context, string handoffPath)
    {
        context.Cancellation.Token.ThrowIfCancellationRequested();
        var content = File.ReadAllText(handoffPath);
        var exported = EnvGenerator.Parse(content);

        if (!exported.TryGetValue("ALEXANDRIA_GENERATION_ID", out var generation) || generation != context.Config.GenerationId)
        {
            throw new InvalidOperationException("The storage key handoff belongs to another generation. Keep it for recovery and rerun the storage initialization job for this installation.");
        }

        var credentials = GarageCredentialsFileParser.Parse(content)
            ?? throw new InvalidOperationException("The storage key handoff is incomplete. It was preserved; rerun storage initialization before starting the application.");
        var envPath = Path.Combine(context.InstallPath, ".env");
        var existingContent = File.ReadAllText(envPath);
        var values = EnvGenerator.Parse(existingContent);
        RequireGeneration(values, context.Config.GenerationId);

        foreach (var (key, value) in GarageCredentialsFileParser.ToEnvValues(credentials))
        {
            if (values.TryGetValue(key, out var established) &&
                !IsPending(established) && established != value)
            {
                throw new InvalidOperationException($"Storage initialization disagrees with the established '{key}'. Both copies were preserved; recover the matching storage credentials before continuing.");
            }

            values[key] = value;
        }

        EnvGenerator.WriteFile(envPath, values, existingContent);
        var persisted = EnvGenerator.Parse(File.ReadAllText(envPath));

        if (values.Any(pair => !persisted.TryGetValue(pair.Key, out var value) || value != pair.Value))
        {
            throw new IOException("Storage keys could not be verified after persistence. The handoff was retained for recovery.");
        }

        PublishReceipt(context, persisted);
        context.Config.EnvironmentValues = persisted;
        context.Config.Credentials = new Dictionary<string, string>(persisted);
        context.GarageCredentials = credentials;
        File.Delete(handoffPath);
    }

    public static bool AcknowledgePersistedKeys(InstallationContext context)
    {
        context.Cancellation.Token.ThrowIfCancellationRequested();
        var envPath = Path.Combine(context.InstallPath, ".env");

        if (!File.Exists(envPath)) return false;

        var persisted = EnvGenerator.Parse(File.ReadAllText(envPath));

        if (!HasCompleteKeys(persisted)) return false;

        RequireGeneration(persisted, context.Config.GenerationId);

        foreach (var key in GarageCredentialsFileParser.ToEnvValues(new("", "", "", "", "", "")).Keys)
        {
            if (context.Config.EnvironmentValues.TryGetValue(key, out var established) &&
                !IsPending(established) && established != persisted[key])
                throw new InvalidOperationException("The persisted storage keys changed after review. Existing files were preserved; review the matching credentials before continuing.");
        }

        PublishReceipt(context, persisted);
        context.Config.EnvironmentValues = persisted;
        context.Config.Credentials = new Dictionary<string, string>(persisted);
        return true;
    }

    private static void PublishReceipt(InstallationContext context, IReadOnlyDictionary<string, string> persisted)
    {
        context.Cancellation.Token.ThrowIfCancellationRequested();
        RequireGeneration(persisted, context.Config.GenerationId);

        if (!HasCompleteKeys(persisted))
            throw new InvalidOperationException("The persisted storage key set is incomplete. The handoff was retained for recovery.");

        var receiptPath = Path.Combine(context.InstallPath, "init-output", ReceiptFileName);

        if (new FileInfo(receiptPath).LinkTarget is not null)
            throw new InvalidOperationException("The storage credential receipt must be a regular file. The handoff was retained for recovery.");

        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        var receipt = context.Config.GenerationId + "\n";
        ProtectedFile.WriteAtomic(receiptPath, receipt, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        if (File.ReadAllText(receiptPath) != receipt)
            throw new IOException("Storage credential persistence could not be acknowledged. The handoff was retained for recovery.");
    }

    private static void RequireGeneration(IReadOnlyDictionary<string, string> values, string generation)
    {
        if (string.IsNullOrWhiteSpace(generation) || generation.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_') ||
            values.GetValueOrDefault("ALEXANDRIA_GENERATION_ID") != generation)
            throw new InvalidOperationException("The persisted environment belongs to another or an invalid generation. Existing files and the storage handoff were preserved.");
    }

    private static bool IsPending(string value) => string.IsNullOrWhiteSpace(value) ||
        value.Equals("placeholder", StringComparison.OrdinalIgnoreCase);
}
