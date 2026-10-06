using Alexandria.Common;
using Alexandria.Dto.Files.Streaming.Stats;

namespace Alexandria.Services.Streaming.Wrapped;

internal static class ListeningSessionRowExtensions
{
    public static bool IsQualifiedPlay(this ListeningSessionRow row)
        => row.QualifiedPlay ?? ListeningPlayQualification.IsQualified(row.ListenedSeconds, row.DurationSeconds);
}
