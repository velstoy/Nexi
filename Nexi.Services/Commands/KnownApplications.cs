namespace Nexi.Services.Commands;

/// <summary>
/// Catalog of applications Nexi knows how to launch, keyed by lowercase alias.
/// Values carry the Windows executable, the macOS app name and the Linux command.
/// </summary>
public static class KnownApplications
{
    public sealed record Entry(string Alias, string DisplayName, string WindowsExe, string MacApp, string LinuxCommand);

    private static readonly Dictionary<string, Entry> Apps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["calculator"] = new("calculator", "Calculator", "calc.exe", "Calculator", "gnome-calculator"),
        ["calc"] = new("calculator", "Calculator", "calc.exe", "Calculator", "gnome-calculator"),
        ["notepad"] = new("notepad", "Notepad", "notepad.exe", "TextEdit", "gedit"),
        ["paint"] = new("paint", "Paint", "mspaint.exe", "Paint", "gnome-paint"),
        ["explorer"] = new("explorer", "File Explorer", "explorer.exe", "Finder", "nautilus"),
        ["files"] = new("explorer", "Files", "explorer.exe", "Finder", "nautilus"),
        ["terminal"] = new("terminal", "Terminal", "wt.exe", "Terminal", "gnome-terminal"),
        ["chrome"] = new("chrome", "Google Chrome", "chrome.exe", "Google Chrome", "google-chrome"),
        ["edge"] = new("edge", "Microsoft Edge", "msedge.exe", "Microsoft Edge", "microsoft-edge"),
        ["firefox"] = new("firefox", "Firefox", "firefox.exe", "Firefox", "firefox"),
        ["vscode"] = new("vscode", "Visual Studio Code", "code", "Visual Studio Code", "code"),
        ["code"] = new("vscode", "Visual Studio Code", "code", "Visual Studio Code", "code"),
        ["spotify"] = new("spotify", "Spotify", "spotify.exe", "Spotify", "spotify"),
        ["discord"] = new("discord", "Discord", "discord.exe", "Discord", "discord"),
        ["steam"] = new("steam", "Steam", "steam.exe", "Steam", "steam"),
        ["zoom"] = new("zoom", "Zoom", "zoom.exe", "zoom.us", "zoom"),
        ["outlook"] = new("outlook", "Outlook", "outlook.exe", "Microsoft Outlook", "thunderbird"),
        ["word"] = new("word", "Word", "winword.exe", "Microsoft Word", "libreoffice --writer"),
        ["excel"] = new("excel", "Excel", "excel.exe", "Microsoft Excel", "libreoffice --calc"),
        ["powerpoint"] = new("powerpoint", "PowerPoint", "powerpnt.exe", "Microsoft PowerPoint", "libreoffice --impress"),
        ["whatsapp"] = new("whatsapp", "WhatsApp", "whatsapp.exe", "WhatsApp", "whatsapp-desktop"),
        ["telegram"] = new("telegram", "Telegram", "telegram.exe", "Telegram", "telegram-desktop"),
    };

    /// <summary>Common website aliases so "open github" means https://github.com.</summary>
    private static readonly Dictionary<string, string> Sites = new(StringComparer.OrdinalIgnoreCase)
    {
        ["github"] = "https://github.com",
        ["google"] = "https://www.google.com",
        ["youtube"] = "https://www.youtube.com",
        ["gmail"] = "https://mail.google.com",
        ["reddit"] = "https://www.reddit.com",
        ["stackoverflow"] = "https://stackoverflow.com",
        ["wikipedia"] = "https://www.wikipedia.org",
        ["amazon"] = "https://www.amazon.com",
        ["twitch"] = "https://www.twitch.tv",
        ["x"] = "https://x.com",
        ["twitter"] = "https://x.com",
        ["maps"] = "https://maps.google.com",
        ["weather"] = "https://weather.com",
    };

    public static bool TryGet(string alias, out Entry entry) => Apps.TryGetValue(alias, out entry!);

    public static IReadOnlyCollection<string> Aliases => Apps.Keys;

    public static bool IsKnownSite(string alias) => Sites.ContainsKey(alias);

    public static string? SiteUrl(string alias) => Sites.TryGetValue(alias, out var url) ? url : null;
}
