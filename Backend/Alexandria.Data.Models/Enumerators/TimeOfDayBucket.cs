namespace Alexandria.Data.Models.Enumerators;

/// <summary>
/// Time-of-day listening persona bucket (D5: UTC hour of session start).
/// </summary>
public enum TimeOfDayBucket
{
    Night = 0,
    Morning = 1,
    Afternoon = 2,
    Evening = 3,
}