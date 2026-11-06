using System.Diagnostics;

namespace Alexandria.Tests.Unit.Common;

/// <summary>
/// Lets process tests spawn a recognisable <c>sleep</c> and check in /proc whether it is gone.
/// </summary>
internal static class SleepProbe
{
    // The fractional part makes the duration unique, so the process can be identified in /proc.
    public static string UniqueSleepDuration(int seconds) =>
        $"{seconds}.{Random.Shared.Next(100_000, 999_999)}";

    // SIGKILL is delivered at once, but the kernel can take a moment to tear the process down.
    public static async Task<bool> SleepStopsWithinAsync(string duration, TimeSpan limit)
    {
        var watch = Stopwatch.StartNew();

        while (watch.Elapsed < limit)
        {
            if (!IsSleepRunning(duration))
                return true;

            await Task.Delay(25, CancellationToken.None);
        }

        return false;
    }

    private static bool IsSleepRunning(string duration)
    {
        var expected = $"sleep\0{duration}\0";

        foreach (var directory in Directory.GetDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(directory), out _))
                continue;

            try
            {
                var isZombie = File.ReadAllText($"{directory}/stat").Contains(") Z ");
                if (!isZombie && File.ReadAllText($"{directory}/cmdline") == expected)
                    return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Exited while being inspected, or belongs to another user: not the one we want.
                continue;
            }
        }

        return false;
    }
}
