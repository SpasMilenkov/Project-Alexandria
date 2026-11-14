using Spectre.Console.Rendering;

namespace Builder.UI.Shell.Input;

public sealed class TextFieldSpec
{
    public required string Label { get; init; }
    public string Initial { get; init; } = string.Empty;
    public bool Secret { get; init; }

    // Returns null when the value is valid, otherwise the message to show
    public Func<string, string?>? Validator { get; init; }
    public string? Hint { get; init; }

    // Drawn under the field on every keystroke from the text typed so far,
    // for feedback that should not wait for Enter
    public Func<string, IRenderable?>? Preview { get; init; }
}
