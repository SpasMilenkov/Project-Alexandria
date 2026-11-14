using Builder.SystemCheck;
using Builder.UI.Shell.Sidebar;

namespace Builder.UI.Shell.Screens.Components;

// Judges what Docker can give against what the selection needs, with the
// same limits the resource warnings use, so a row and its warning agree
internal static class MachineFit
{
    public static CheckState Memory(long availableMb, long neededMb)
    {
        if (availableMb <= 0) return CheckState.Unknown;

        if (availableMb < neededMb) return CheckState.Failed;

        if (availableMb < neededMb * ResourceCalculator.MemoryComfortFactor) return CheckState.Warning;

        return CheckState.Passed;
    }

    public static CheckState Cpu(int cores, int recommended)
    {
        if (cores <= 0) return CheckState.Unknown;

        return cores < recommended ? CheckState.Warning : CheckState.Passed;
    }

    public static CheckState Disk(long? freeMb)
    {
        if (freeMb is not { } free) return CheckState.Unknown;

        return free < ResourceCalculator.MinimumDockerDiskMb ? CheckState.Warning : CheckState.Passed;
    }

    public static StatTone Tone(CheckState state) => state switch
    {
        CheckState.Failed => StatTone.Error,
        CheckState.Warning => StatTone.Warning,
        _ => StatTone.Normal,
    };
}
