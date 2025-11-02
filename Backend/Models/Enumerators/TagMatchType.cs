namespace Models.Enumerators;

public enum TagMatchType
{
    Any,    // Files that have ANY of the specified tags
    All,    // Files that have ALL of the specified tags
    Exact   // Files that have EXACTLY these tags (no more, no less)
}