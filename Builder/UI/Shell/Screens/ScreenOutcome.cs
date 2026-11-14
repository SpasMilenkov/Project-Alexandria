namespace Builder.UI.Shell.Screens;

public enum ScreenOutcome
{
    Continue,
    Back,
    Abort,

    // Nothing to ask with the current choices; the walk keeps going the way
    // it was already moving, so stepping back passes through too
    Skip,
}
