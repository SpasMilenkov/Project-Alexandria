namespace Builder.UI.Shell;

public sealed class TextFieldSpec
{
    public required string Label { get; init; }
    public string Initial { get; init; } = string.Empty;
    public bool Secret { get; init; }

    // Returns null when the value is valid, otherwise the message to show
    public Func<string, string?>? Validator { get; init; }
    public string? Hint { get; init; }
}
