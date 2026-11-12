namespace Alexandria.Dto.Audit;

public sealed class FailedSignInSource
{
    public required string IpAddress { get; init; }
    public required int Count { get; init; }

    /// <summary>Distinct accounts tried from this address; unknown emails are not counted.</summary>
    public required int AccountCount { get; init; }

    public required DateTimeOffset LastSeen { get; init; }
}
