using Common.Settings.Values;

namespace API.Features.Settings.Behavior
{
    public class GetBehaviorResponse
    {
        public bool SkipDeleteConfirmation { get; set; }
        public ToastLevel ToastLevel { get; set; }
    }
}
