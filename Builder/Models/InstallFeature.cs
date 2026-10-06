namespace Builder.Models;

public sealed class InstallFeature
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public required string Cost { get; init; }
    public bool DefaultEnabled { get; init; }
}
