using Nexi.Data.Models;

namespace Nexi.Services.Commands;

/// <summary>
/// Decides whether a desktop command may run freely, needs explicit user confirmation,
/// or must be blocked outright.
/// </summary>
public static class CommandSafetyClassifier
{
    private static readonly string[] DangerousScriptTokens =
    [
        "rm -rf /", "rm -rf ~", "rm -rf *", "rm -rf .",
        "del /s /q", "format ", "mkfs", "dd if=", ":(){",
        "shutdown", "restart-computer", "reg delete", "cipher /w",
        "diskpart", "> $profile", "invoke-webrequest",
        "iex(", "irm ", "curl | iex", "wget | sh"
    ];

    public static CommandSafety Classify(DesktopCommand command) => command.Kind switch
    {
        CommandKind.OpenUrl => IsSafeHttpUrl(command.Argument) ? CommandSafety.Safe : CommandSafety.Blocked,
        CommandKind.OpenApplication => KnownApplications.TryGet(command.Argument, out _)
            ? CommandSafety.Safe
            : CommandSafety.NeedsConfirmation,
        CommandKind.OpenPath => ClassifyPath(command.Argument),
        CommandKind.RunScript => IsDangerousScript(command.Argument)
            ? CommandSafety.Blocked
            : CommandSafety.NeedsConfirmation,
        CommandKind.GetSystemInfo or CommandKind.GetTime or CommandKind.ShowMessage => CommandSafety.Safe,
        _ => CommandSafety.NeedsConfirmation
    };

    public static bool IsSafeHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrWhiteSpace(uri.Host);

    public static bool IsDangerousScript(string? script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return false;
        }

        var lowered = script.ToLowerInvariant();
        return DangerousScriptTokens.Any(lowered.Contains);
    }

    private static CommandSafety ClassifyPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return CommandSafety.Blocked;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return CommandSafety.Blocked;
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var underProfile = fullPath.StartsWith(profile, StringComparison.OrdinalIgnoreCase);

        return underProfile ? CommandSafety.Safe : CommandSafety.NeedsConfirmation;
    }
}
