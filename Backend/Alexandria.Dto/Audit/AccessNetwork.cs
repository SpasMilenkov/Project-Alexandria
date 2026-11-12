namespace Alexandria.Dto.Audit;

/// <summary>An address accounts signed in from inside the window.</summary>
public sealed class AccessNetwork
{
    public required string IpAddress { get; init; }
    public required int AccountCount { get; init; }
    public required int SignIns { get; init; }

    /// <summary>First sign-in from this address ever recorded, not just inside the window.</summary>
    public required DateTimeOffset FirstSeen { get; init; }

    public required DateTimeOffset LastSeen { get; init; }
}
