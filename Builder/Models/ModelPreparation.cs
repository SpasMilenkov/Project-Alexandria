namespace Builder.Models;

public sealed record ModelAssetDefinition(string FileName, long Bytes, string Sha256, string DownloadPath);

public sealed record ModelPreparationPlan(
    string SourceDirectory,
    string ModelDirectory,
    string? Backbone,
    IReadOnlyList<ModelAssetDefinition> Assets)
{
    public long ExpectedBytes => Assets.Sum(asset => asset.Bytes);
}

public sealed record ModelPreparationProgress(
    string Stage,
    string FileName,
    int FileIndex,
    int FileCount,
    long CompletedBytes,
    long ExpectedBytes,
    int Attempt = 1);

public sealed record ModelPreparationResult(string ModelDirectory, int ReusedFiles, int PreparedFiles, long VerifiedBytes);
