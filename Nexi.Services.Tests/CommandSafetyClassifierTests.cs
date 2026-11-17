using Nexi.Data.Models;
using Nexi.Services.Commands;

namespace Nexi.Services.Tests;

public class CommandSafetyClassifierTests
{
    [Theory]
    [InlineData("https://example.com", CommandSafety.Safe)]
    [InlineData("http://localhost:8080/admin", CommandSafety.Safe)]
    [InlineData("file:///etc/passwd", CommandSafety.Blocked)]
    [InlineData("ftp://example.com", CommandSafety.Blocked)]
    [InlineData("not a url", CommandSafety.Blocked)]
    public void Url_classification(string url, CommandSafety expected)
    {
        var command = new DesktopCommand { Kind = CommandKind.OpenUrl, Argument = url };
        Assert.Equal(expected, CommandSafetyClassifier.Classify(command));
    }

    [Theory]
    [InlineData("notepad", CommandSafety.Safe)]
    [InlineData("chrome", CommandSafety.Safe)]
    [InlineData("some-random-app", CommandSafety.NeedsConfirmation)]
    public void Application_classification(string app, CommandSafety expected)
    {
        var command = new DesktopCommand { Kind = CommandKind.OpenApplication, Argument = app };
        Assert.Equal(expected, CommandSafetyClassifier.Classify(command));
    }

    [Theory]
    [InlineData("echo hello", CommandSafety.NeedsConfirmation)]
    [InlineData("rm -rf /", CommandSafety.Blocked)]
    [InlineData("format c:", CommandSafety.Blocked)]
    [InlineData("shutdown /s /t 0", CommandSafety.Blocked)]
    [InlineData("del /s /q C:\\", CommandSafety.Blocked)]
    public void Script_classification(string script, CommandSafety expected)
    {
        var command = new DesktopCommand { Kind = CommandKind.RunScript, Argument = script };
        Assert.Equal(expected, CommandSafetyClassifier.Classify(command));
    }

    [Fact]
    public void Path_under_user_profile_is_safe()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var command = new DesktopCommand { Kind = CommandKind.OpenPath, Argument = Path.Combine(profile, "Documents") };
        Assert.Equal(CommandSafety.Safe, CommandSafetyClassifier.Classify(command));
    }

    [Fact]
    public void Path_outside_user_profile_needs_confirmation()
    {
        var command = new DesktopCommand { Kind = CommandKind.OpenPath, Argument = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))! };
        Assert.Equal(CommandSafety.NeedsConfirmation, CommandSafetyClassifier.Classify(command));
    }

    [Fact]
    public void Invalid_path_is_blocked()
    {
        var command = new DesktopCommand { Kind = CommandKind.OpenPath, Argument = "  " };
        Assert.Equal(CommandSafety.Blocked, CommandSafetyClassifier.Classify(command));
    }

    [Theory]
    [InlineData(CommandKind.GetSystemInfo)]
    [InlineData(CommandKind.GetTime)]
    [InlineData(CommandKind.ShowMessage)]
    public void Info_commands_are_safe(CommandKind kind)
    {
        var command = new DesktopCommand { Kind = kind, Argument = "x" };
        Assert.Equal(CommandSafety.Safe, CommandSafetyClassifier.Classify(command));
    }
}
