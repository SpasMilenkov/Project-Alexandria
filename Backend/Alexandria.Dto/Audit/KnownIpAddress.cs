namespace Alexandria.Dto.Audit;

public sealed class KnownIpAddress
{
    public required string IpAddress { get; init; }
    public required int LoginCount { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public required DateTimeOffset LastSeen { get; init; }
}
