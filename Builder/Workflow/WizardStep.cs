using Builder.UI;

namespace Builder.Workflow;

public abstract class WizardStep
{
    public abstract string Title { get; }
    public abstract int StepNumber { get; }
    public int TotalSteps { get; set; } = 8;

    public void Execute(InstallationContext context)
    {
        Components.StepHeader(StepNumber, TotalSteps, Title);

        try
        {
            Run(context);
        }
        catch (Exception ex)
        {
            Components.ErrorPanel($"Step failed: {ex.Message}");
            context.ShouldAbort = true;
        }
    }

    protected abstract void Run(InstallationContext context);
}
