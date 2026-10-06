using AwesomeAssertions;
using Builder.Tests.Support;
using Builder.UI;
using Builder.UI.Shell;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Builder.Tests.Workflow;

[Collection(nameof(StaticConsoleTests))]
public class TextEditingTests
{
    [Fact]
    public void AskText_prefilledValue_appendsSuffix()
    {
        using var harness = new ShellHarness(new FakeKeys().Text("/settings").Enter());

        var value = AskText(harness, "/srv/alexandria");

        value.Should().Be("/srv/alexandria/settings");
    }

    [Fact]
    public void AskText_backspaceThenSuffix_editsPrefilledValue()
    {
        using var harness = new ShellHarness(new FakeKeys().Backspaces(3).Text("8443").Enter());

        var value = AskText(harness, "host:443");

        value.Should().Be("host:8443");
    }

    [Fact]
    public void AskText_navigationAndDeletion_editsMiddleAndEnd()
    {
        var keys = new FakeKeys()
            .Press(ConsoleKey.Home).Right().Right()
            .Text("XY").Left().Press(ConsoleKey.Delete)
            .Press(ConsoleKey.End).Backspace().Enter();

        using var harness = new ShellHarness(keys);

        var value = AskText(harness, "abcdef");

        value.Should().Be("abXcde");
        harness.Output.Should().Contain("abX▏Ycdef");
    }

    [Fact]
    public void AskText_clearThenType_resetsValueAndCursor()
    {
        var keys = new FakeKeys().Left().Press(ConsoleKey.U, '\u0015', ctrl: true)
            .Left().Backspace().Press(ConsoleKey.Delete).Text("new").Enter();

        using var harness = new ShellHarness(keys);

        var value = AskText(harness, "prefilled");

        value.Should().Be("new");
    }

    [Fact]
    public void AskText_enterWithoutEditing_keepsInitialValue()
    {
        using var harness = new ShellHarness(new FakeKeys().Enter());

        AskText(harness, "unchanged").Should().Be("unchanged");
    }

    [Fact]
    public void AskText_validationFailure_preservesValueAndCursorForCorrection()
    {
        var attempts = new List<string>();
        var keys = new FakeKeys().Left().Enter().Text("x").Enter();

        using var harness = new ShellHarness(keys);
        harness.Console.Profile.Width = 140;

        var value = Widgets.AskText(harness.Shell, new Markup("Header"), new TextFieldSpec
        {
            Label = "Value",
            Initial = "ab",
            Validator = current =>
            {
                attempts.Add(current);

                return current == "axb" ? null : "Expected [middle] correction";
            },
        });

        value.Should().Be("axb");
        attempts.Should().Equal("ab", "axb");
        harness.Output.Should().Contain("Expected [middle] correction");
    }

    [Fact]
    public void AskText_escape_signalsBackWithoutValidation()
    {
        using var harness = new ShellHarness(new FakeKeys().Text("partial").Press(ConsoleKey.Escape));
        var validated = false;

        var value = Widgets.AskText(harness.Shell, new Markup("Header"), new TextFieldSpec
        {
            Label = "Value",
            Initial = "prefix-",
            Validator = _ =>
            {
                validated = true;

                return null;
            },
        });

        value.Should().Be("prefix-partial");
        validated.Should().BeFalse();
        harness.Shell.TakeEscape().Should().BeTrue();
        harness.Shell.TakeEscape().Should().BeFalse();
        harness.Shell.GlobalsSuppressed.Should().BeFalse();
    }

    [Fact]
    public void AskText_secretValue_masksAllEditingFrames()
    {
        var keys = new FakeKeys().Left().Backspace().Text("T😀").Press(ConsoleKey.Home)
            .Press(ConsoleKey.Delete).Enter();

        using var harness = new ShellHarness(keys);

        var value = AskText(harness, "sëcret", secret: true);

        value.Should().Be("ëcrT😀t");
        harness.Output.Should().Contain("•▏•");
        harness.Output.Should().NotContain("sëcret");
        harness.Output.Should().NotContain("ëcr");
        harness.Output.Should().NotContain("😀");
        harness.Output.Should().NotContain("T");
    }

    [Fact]
    public void AskText_literalT_doesNotCycleTheme()
    {
        using var harness = new ShellHarness(new FakeKeys().Text("Tt").Enter());
        var initialTheme = Theme.Active.Id;
        var saved = false;
        harness.Shell.ThemeSaver = _ => saved = true;

        var value = AskText(harness, "value");

        value.Should().Be("valueTt");
        Theme.Active.Id.Should().Be(initialTheme);
        saved.Should().BeFalse();
    }

    [Fact]
    public void AskText_ctrlC_abortsAndRestoresGlobalKeys()
    {
        using var harness = new ShellHarness(new FakeKeys().Press(ConsoleKey.C, '\u0003', ctrl: true));
        var aborted = false;
        harness.Shell.OnAbort = () => aborted = true;

        var action = () => AskText(harness, "draft");

        action.Should().Throw<ShellAbortException>();
        aborted.Should().BeTrue();
        harness.Shell.GlobalsSuppressed.Should().BeFalse();
    }

    [Fact]
    public void AskText_unicodeInputAndNavigation_preservesSurrogatePairs()
    {
        var keys = new FakeKeys().Left().Backspace().Text("😀").Left()
            .Press(ConsoleKey.Delete).Text("é").Enter();

        using var harness = new ShellHarness(keys);

        var value = AskText(harness, "/café/文件😀x");

        value.Should().Be("/café/文件éx");
        value.Any(char.IsSurrogate).Should().BeFalse();
    }

    [Fact]
    public void AskText_rightArrowAcrossSurrogatePair_insertsAfterWholeCharacter()
    {
        var keys = new FakeKeys().Press(ConsoleKey.Home).Right().Text("é").Enter();

        using var harness = new ShellHarness(keys);

        AskText(harness, "😀x").Should().Be("😀éx");
    }

    [Fact]
    public void AskText_typedSupplementaryCharacter_insertsOneValidPair()
    {
        using var harness = new ShellHarness(new FakeKeys().Text("😀").Enter());

        AskText(harness, "路径-").Should().Be("路径-😀");
    }

    [Fact]
    public void AskText_userMarkup_rendersLiteralText()
    {
        using var harness = new ShellHarness(new FakeKeys().Text("[/]tail").Enter());

        var value = AskText(harness, "[red]path");

        value.Should().Be("[red]path[/]tail");
        harness.Output.Should().Contain("[red]path[/]tail");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(20)]
    public void TextEditor_longUnicodeValue_fitsViewportAndShowsCursor(int width)
    {
        var editor = new TextEditor("/café/文件/😀/a-very-long-path/settings", secret: false);

        var rendered = Render(editor, width);

        rendered.Should().Contain("▏");

        if (width > 1)
        {
            rendered.Should().Contain("…");
        }

        rendered.GetCellWidth().Should().BeLessThanOrEqualTo(width);
        rendered.Should().NotContain("\n");
        rendered.Should().NotContain("�");
    }

    [Fact]
    public void TextEditor_cursorMovesAndWidthChanges_viewportFollowsCursor()
    {
        var editor = new TextEditor("0123456789abcdefghijklmnop", secret: false);

        Render(editor, 8).Should().EndWith("mnop▏");
        editor.HandleKey(new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false));
        Render(editor, 8).Should().StartWith("▏0123");

        for (var i = 0; i < 13; i++)
        {
            editor.HandleKey(new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false));
        }

        Render(editor, 8).Should().Contain("bc▏de");
        Render(editor, 40).Should().Be("0123456789abc▏defghijklmnop");
    }

    [Fact]
    public void AskText_longPathInNarrowPane_keepsValueEditable()
    {
        var initial = "/a-very-long-directory-name/another-long-directory/settings";
        var keys = new FakeKeys().Press(ConsoleKey.Home).Text("prefix")
            .Press(ConsoleKey.End).Text("/end").Enter();

        using var harness = new ShellHarness(keys);
        harness.Console.Profile.Width = 42;

        var value = AskText(harness, initial);

        value.Should().Be($"prefix{initial}/end");
        harness.Output.Should().Contain("prefix▏");
        harness.Output.Should().Contain("/end▏");
        harness.Output.Split('\n').Where(line => line.Contains('▏')).Should()
            .OnlyContain(line => line.GetCellWidth() <= 42);
    }

    private static string AskText(ShellHarness harness, string initial, bool secret = false)
    {
        return Widgets.AskText(harness.Shell, new Markup("Header"), new TextFieldSpec
        {
            Label = "Value",
            Initial = initial,
            Secret = secret,
        });
    }

    private static string Render(TextEditor editor, int width)
    {
        using var console = new TestConsole();
        console.Profile.Width = width;
        console.Write(editor);

        return console.Output.TrimEnd('\r', '\n');
    }
}
