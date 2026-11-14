namespace Builder.UI.Shell.Screens;

// Where one system check stands. Warning still lets setup continue; Unknown
// means the value could not be measured, which is not a failure.
public enum CheckState
{
    Pending,
    Running,
    Passed,
    Warning,
    Failed,
    Skipped,
    Unknown,
}
