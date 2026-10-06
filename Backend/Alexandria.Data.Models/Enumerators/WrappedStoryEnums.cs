namespace Alexandria.Data.Models.Enumerators;

public enum WrappedTimeScene
{
    Night = 0,
    Morning = 1,
    Midday = 2,
    Afternoon = 3,
    Evening = 4
}

public enum WrappedRhythmEvidence
{
    Sparse = 0,
    Balanced = 1,
    Pronounced = 2
}

public enum WrappedDurationTier
{
    UnderTwoHours = 0,
    TwoToThree = 1,
    ThreeToSix = 2,
    SixToTwelve = 3,
    TwelveToTwenty = 4,
    TwentyToForty = 5,
    FortyToEighty = 6,
    EightyToOneTwenty = 7,
    OneTwentyPlus = 8
}

public enum WrappedDurationCategory
{
    Sport = 0,
    Movie = 1,
    HistoricalEvent = 2,
    MovieSeries = 3,
    Television = 4,
    Everyday = 5,
    Travel = 6,
    Time = 7,
    Space = 8,
    Animation = 9,
    Theatre = 10,
    Audiobook = 11
}

public enum WrappedDurationRelationship
{
    Approximately = 0,
    MilestonePassed = 1,
    WithinRange = 2
}
