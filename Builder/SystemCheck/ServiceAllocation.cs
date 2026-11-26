namespace Builder.SystemCheck;

public sealed record ServiceAllocation
{
    public required string ServiceId { get; init; }
    public required long MemoryMb { get; init; }
    public required double Cpus { get; init; }
}
