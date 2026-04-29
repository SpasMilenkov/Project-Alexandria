using Alexandria.Common.Settings.Values;

namespace Alexandria.Api.Features.Settings.Behavior;

public class GetBehaviorResponse
{
    public bool SkipDeleteConfirmation { get; set; }
    public ToastLevel ToastLevel { get; set; }
}