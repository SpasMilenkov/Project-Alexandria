using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Builder.Models;

namespace Builder.Services;

public sealed class ModelPreparationService
{
    public const string RequirementsFile = "model-requirements.tsv";
    public const int MaximumAttempts = 3;
    private const UnixFileMode ReadableFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    private const UnixFileMode TraversableDirectoryMode = ReadableFileMode | UnixFileMode.UserExecute
        | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
    private readonly HttpClient? _httpClient;
    private readonly bool _allowDownloads;
    private readonly TimeSpan _retryDelay;

    public ModelPreparationService(HttpClient? httpClient = null, bool allowDownloads = false)
        : this(httpClient, allowDownloads, TimeSpan.FromSeconds(1))
    {
    }

    internal ModelPreparationService(HttpClient? httpClient, bool allowDownloads, TimeSpan retryDelay)
    {
        _httpClient = httpClient;
        _allowDownloads = allowDownloads;
        _retryDelay = retryDelay;
    }

    public ModelPreparationPlan CreatePlan(
        string sourceRoot, string targetDirectory, string? backbone, string? sourceModelDirectory = null)
    {
        var cache = Path.GetFullPath(Path.Combine(targetDirectory, "models"));
        var pinnedSource = Path.GetFullPath(Path.Combine(sourceRoot, "classificators", "essentia", "models"));
        var source = Path.GetFullPath(sourceModelDirectory ?? pinnedSource);

        if (string.IsNullOrEmpty(backbone) || backbone == "off")
        {
            return new ModelPreparationPlan(source, cache, null, []);
        }

        if (backbone is not ("effnet" or "maest"))
        {
            throw new ArgumentException("Choose audio backbone 'off', 'effnet' or 'maest'.", nameof(backbone));
        }

        var manifest = ReadChecksums(Path.Combine(pinnedSource, "models.sha256"));
        var requirementsPath = Path.Combine(sourceRoot, "classificators", "essentia", RequirementsFile);
        var assets = new List<ModelAssetDefinition>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(requirementsPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 4 || parts[0] is not ("shared" or "effnet" or "maest")
                || !IsFileName(parts[1]) || !names.Add(parts[1])
                || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) || bytes <= 0
                || !IsDownloadPath(parts[3], parts[1]))
            {
                throw new InvalidDataException($"Invalid or duplicate model requirement in {requirementsPath}.");
            }

            if (!manifest.TryGetValue(parts[1], out var checksum))
            {
                throw new InvalidDataException($"No pinned SHA-256 checksum for model {parts[1]}.");
            }

            if (parts[0] == "shared" || parts[0] == backbone)
            {
                assets.Add(new ModelAssetDefinition(parts[1], bytes, checksum, parts[3]));
            }
        }

        if (assets.Count == 0)
        {
            throw new InvalidDataException($"No model requirements found for {backbone}.");
        }

        return new ModelPreparationPlan(source, cache, backbone, assets);
    }

    public Task<ModelPreparationResult> PrepareAsync(
        string sourceRoot,
        string targetDirectory,
        string? backbone,
        Action<ModelPreparationProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        PrepareAsync(CreatePlan(sourceRoot, targetDirectory, backbone), progress, cancellationToken);

    public async Task<ModelPreparationResult> PrepareAsync(
        ModelPreparationPlan plan,
        Action<ModelPreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (plan.Assets.Count == 0)
        {
            return new ModelPreparationResult(plan.ModelDirectory, 0, 0, 0);
        }

        EnsureDirectory(plan.ModelDirectory);
        var lockPath = Path.Combine(plan.ModelDirectory, ".prepare.lock");
        RejectLink(lockPath);
        await using var preparationLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var reused = 0;
        var prepared = 0;
        long verified = 0;

        foreach (var asset in plan.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(plan.ModelDirectory, asset.FileName);
            RejectLink(target);
            Report("Verifying cache", 0);

            if (await IsVerifiedAsync(target, asset, cancellationToken).ConfigureAwait(false))
            {
                SetFileMode(target);
                reused++;
                verified += asset.Bytes;
                Report("Reused verified cache", 0);
                continue;
            }

            var localSource = Path.Combine(plan.SourceDirectory, asset.FileName);
            RejectLink(localSource);
            var localSourceExists = File.Exists(localSource);
            var useLocal = await IsVerifiedAsync(localSource, asset, cancellationToken).ConfigureAwait(false);

            if (!useLocal && !_allowDownloads)
            {
                if (localSourceExists)
                {
                    throw new InvalidDataException($"SHA-256/size verification failed for {asset.FileName} in {plan.SourceDirectory}; "
                        + "model downloads are disabled.");
                }

                throw new FileNotFoundException(
                    $"Required {plan.Backbone} model {asset.FileName} is missing or corrupt in {plan.ModelDirectory} "
                    + $"and is not available in {plan.SourceDirectory}. Supply existing verified model files and retry; "
                    + "model downloads are disabled.", localSource);
            }

            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                var temporary = Path.Combine(plan.ModelDirectory, $".{asset.FileName}.{Guid.NewGuid():N}.partial");

                try
                {
                    Report(useLocal ? "Copying existing model" : "Downloading model", 0, attempt);
                    await using (var output = CreateTemporary(temporary))
                    {
                        if (useLocal)
                        {
                            await using var input = new FileStream(localSource, FileMode.Open, FileAccess.Read, FileShare.Read,
                                131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
                            await CopyAsync(input, output, asset, copied => Report("Copying existing model", copied, attempt), cancellationToken)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            await DownloadAsync(asset, output, copied => Report("Downloading model", copied, attempt), cancellationToken)
                                .ConfigureAwait(false);
                        }

                        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                        output.Flush(flushToDisk: true);
                    }

                    Report("Verifying prepared model", 0, attempt);

                    if (!await IsVerifiedAsync(temporary, asset, cancellationToken).ConfigureAwait(false))
                    {
                        throw new InvalidDataException($"SHA-256/size verification failed for {asset.FileName}. "
                            + "The unverified file was discarded; verified cached models are retained.");
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    SetFileMode(temporary);
                    RejectLink(target);
                    File.Move(temporary, target, overwrite: true);
                    prepared++;
                    verified += asset.Bytes;
                    Report("Verified model ready", 0, attempt);
                    break;
                }
                catch (Exception error) when (attempt < MaximumAttempts && IsTransient(error))
                {
                    Report("Retrying model preparation", 0, attempt);
                    await Task.Delay(_retryDelay * attempt, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }

            void Report(string stage, long copied, int attempt = 1) => progress?.Invoke(new ModelPreparationProgress(
                stage, asset.FileName, reused + prepared + (stage is "Reused verified cache" or "Verified model ready" ? 0 : 1),
                plan.Assets.Count, verified + copied, plan.ExpectedBytes, attempt));
        }

        var selectedManifest = string.Join('\n', plan.Assets.Select(asset => $"{asset.Sha256}  {asset.FileName}")) + "\n";
        RejectLink(Path.Combine(plan.ModelDirectory, "models.sha256"));
        ProtectedFile.WriteAtomic(Path.Combine(plan.ModelDirectory, "models.sha256"), selectedManifest, ReadableFileMode);

        return new ModelPreparationResult(plan.ModelDirectory, reused, prepared, verified);
    }

    private async Task DownloadAsync(ModelAssetDefinition asset, Stream output, Action<long> progress, CancellationToken ct)
    {
        using var ownedClient = _httpClient is null
            ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan }
            : null;
        var client = _httpClient ?? ownedClient!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            using var response = await client.GetAsync(new Uri($"https://essentia.upf.edu/models/{asset.DownloadPath}"),
                HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            await CopyAsync(input, output, asset, progress, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException error) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException($"Model {asset.FileName} transfer timed out.", error);
        }
    }

    private static async Task CopyAsync(Stream input, Stream output, ModelAssetDefinition asset, Action<long> progress, CancellationToken ct)
    {
        var buffer = new byte[131072];
        long copied = 0;
        int count;

        while ((count = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            copied += count;

            if (copied > asset.Bytes)
            {
                throw new InvalidDataException($"Model {asset.FileName} exceeds its pinned size of {asset.Bytes} bytes.");
            }

            await output.WriteAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
            progress(copied);
        }
    }

    private static async Task<bool> IsVerifiedAsync(string path, ModelAssetDefinition asset, CancellationToken ct)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != asset.Bytes) return false;
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            131072, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = await SHA256.HashDataAsync(input, ct).ConfigureAwait(false);
        return Convert.ToHexString(digest).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ReadChecksums(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2 || parts[0].Length != 64 || parts[0].Any(character => !Uri.IsHexDigit(character))
                || !IsFileName(parts[1]) || !values.TryAdd(parts[1], parts[0].ToLowerInvariant()))
            {
                throw new InvalidDataException($"Invalid or duplicate pinned model checksum in {path}.");
            }
        }

        return values;
    }

    private static bool IsFileName(string value) => value.Length > 0 && value != "." && value != ".."
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static bool IsDownloadPath(string value, string fileName) => value.Split('/').All(IsFileName)
        && value.EndsWith('/' + fileName, StringComparison.Ordinal);

    private static bool IsTransient(Exception error) => error is IOException
        || error is HttpRequestException { StatusCode: null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests }
        || error is HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError };

    private static void EnsureDirectory(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (parent is not null) RejectLink(parent);
        RejectLink(path);
        Directory.CreateDirectory(path);

        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, TraversableDirectoryMode);
    }

    private static FileStream CreateTemporary(string path)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
            Share = FileShare.None, Options = FileOptions.Asynchronous | FileOptions.SequentialScan };

        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    private static void SetFileMode(string path)
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, ReadableFileMode);
    }

    private static void RejectLink(string path)
    {
        if (new FileInfo(path).LinkTarget is not null || new DirectoryInfo(path).LinkTarget is not null)
        {
            throw new IOException($"Refusing symbolic link in model preparation path {path}.");
        }
    }
}
