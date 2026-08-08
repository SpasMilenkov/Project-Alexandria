namespace Alexandria.Common.Exceptions.Policies;

public sealed class AutoTaggingDisabledException : Exception
{
    public AutoTaggingDisabledException()
        : base("Auto-tagging is disabled.")
    {
    }
}