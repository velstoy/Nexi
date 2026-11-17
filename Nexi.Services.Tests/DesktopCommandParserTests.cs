using Nexi.Data.Models;
using Nexi.Services.Commands;

namespace Nexi.Services.Tests;

public class DesktopCommandParserTests
{
    [Fact]
    public void Parses_fenced_json_block()
    {
        const string text = """
            Sure, opening that now.
            ```json
            {"kind": "OpenUrl", "argument": "https://example.com", "explanation": "Open example.com"}
            ```
            """;

        var command = DesktopCommandParser.TryParse(text);

        Assert.NotNull(command);
        Assert.Equal(CommandKind.OpenUrl, command!.Kind);
        Assert.Equal("https://example.com", command.Argument);
        Assert.Equal("Open example.com", command.Explanation);
        Assert.NotNull(command.RawJson);
    }

    [Fact]
    public void Parses_bare_json_with_kind()
    {
        const string text = """Here you go: {"kind":"GetTime","argument":""}""";

        var command = DesktopCommandParser.TryParse(text);

        Assert.NotNull(command);
        Assert.Equal(CommandKind.GetTime, command!.Kind);
    }

    [Fact]
    public void Kind_matching_is_case_insensitive()
    {
        var command = DesktopCommandParser.TryParse("""{"kind":"openapplication","argument":"notepad"}""");
        Assert.NotNull(command);
        Assert.Equal(CommandKind.OpenApplication, command!.Kind);
    }

    [Fact]
    public void Returns_null_when_no_json_present()
    {
        Assert.Null(DesktopCommandParser.TryParse("Just a normal answer."));
        Assert.Null(DesktopCommandParser.TryParse(""));
        Assert.Null(DesktopCommandParser.TryParse(null));
    }

    [Fact]
    public void Returns_null_for_malformed_json()
    {
        Assert.Null(DesktopCommandParser.TryParse("```json {\"kind\": } ```"));
    }

    [Fact]
    public void Returns_null_for_unknown_kind()
    {
        Assert.Null(DesktopCommandParser.TryParse("""{"kind":"FlyToMoon","argument":"x"}"""));
        Assert.Null(DesktopCommandParser.TryParse("""{"kind":"None","argument":"x"}"""));
    }
}
