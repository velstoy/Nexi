using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using Nexi.Data.Models;
using Nexi.Services.Commands;

namespace Nexi.Services.AI;

/// <summary>
/// Built-in, fully offline assistant. Matches common intents with rules and returns a
/// <see cref="DesktopCommand"/> directly, so Nexi stays functional with no model and no API key.
/// </summary>
public sealed class LocalAssistantProvider : IAssistantProvider
{
    private readonly ILogger<LocalAssistantProvider> _logger;

    public LocalAssistantProvider(ILogger<LocalAssistantProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "Nexi Local Assistant";

    private static readonly Regex Greeting = new(
        @"^(hi|hello|hey|yo|hiya|good\s?(morning|afternoon|evening))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TimeQuery = new(
        @"\b(what('| i)s the (time|date)|current (time|date)|what day is it|\btime\b|\bdate\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SystemInfoQuery = new(
        @"\b(system info|sysinfo|cpu|processor|ram|memory|battery|disk|storage|how much (ram|memory|space|storage))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SearchQuery = new(
        @"^(?:search(?:\s+for)?|look up|google|duckduckgo)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OpenQuery = new(
        @"^(open|launch|start|go to|navigate to|visit)\s+(.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RunQuery = new(
        @"^(run|execute|run script|run command)\s+(.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HelpQuery = new(
        @"^(help|what can you do\??|how do you work\??)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DomainLike = new(
        @"^[\w-]+(\.[\w-]+)+(/\S*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PathLike = new(
        @"(\\|/|~|\.[a-z0-9]{1,5}$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<AssistantReply> ReplyAsync(
        IReadOnlyList<ChatMessage> conversation,
        AppSettings settings,
        CancellationToken ct = default)
    {
        var input = conversation.LastOrDefault(m => m.Role == ChatRole.User)?.Content.Trim() ?? string.Empty;
        _logger.LogDebug("Local assistant handling input: {Input}", input);

        var reply = Handle(input);
        return Task.FromResult(new AssistantReply
        {
            Text = reply.Text,
            Command = reply.Command,
            ProviderName = Name
        });
    }

    private static (string Text, DesktopCommand? Command) Handle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ("Say something and I'll try to help.", null);
        }

        if (RunQuery.IsMatch(input))
        {
            var script = RunQuery.Match(input).Groups[2].Value.Trim().Trim('"');
            var command = new DesktopCommand
            {
                Kind = CommandKind.RunScript,
                Argument = script,
                Explanation = $"Run script: {script}"
            };
            if (CommandSafetyClassifier.IsDangerousScript(script))
            {
                command.Safety = CommandSafety.Blocked;
                return ($"I can't run that - it looks destructive. Refusing: {script}", command);
            }

            command.Safety = CommandSafety.NeedsConfirmation;
            return ($"I can run that for you, but it needs your confirmation first: {script}", command);
        }

        if (HelpQuery.IsMatch(input))
        {
            return (Capabilities, new DesktopCommand
            {
                Kind = CommandKind.ShowMessage,
                Argument = Capabilities,
                Safety = CommandSafety.Safe
            });
        }

        if (Greeting.IsMatch(input))
        {
            return ("Hey! I'm Nexi. Ask me to open a website, app or folder, search the web, check the time or system info.", null);
        }

        if (TimeQuery.IsMatch(input))
        {
            return ($"It's {DateTime.Now:F} right now.", new DesktopCommand
            {
                Kind = CommandKind.GetTime,
                Argument = string.Empty,
                Explanation = "Tell the current time",
                Safety = CommandSafety.Safe
            });
        }

        if (SystemInfoQuery.IsMatch(input))
        {
            return ("Gathering system information...", new DesktopCommand
            {
                Kind = CommandKind.GetSystemInfo,
                Argument = string.Empty,
                Explanation = "Collect system information",
                Safety = CommandSafety.Safe
            });
        }

        if (SearchQuery.IsMatch(input))
        {
            var query = SearchQuery.Match(input).Groups[1].Value.Trim();
            var url = $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}";
            return ($"Searching the web for \"{query}\"...", UrlCommand(url, $"Search for {query}"));
        }

        if (OpenQuery.IsMatch(input))
        {
            var target = OpenQuery.Match(input).Groups[2].Value.Trim().Trim('"');
            return OpenTarget(target);
        }

        return ("I'm not sure how to handle that. I can open websites, apps and folders, search the web, " +
                "show the time or system info, and run scripts (with your confirmation). Try \"open github.com\".",
            null);
    }

    private static (string Text, DesktopCommand? Command) OpenTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return ("Open what, exactly?", null);
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return ($"Opening {uri}...", UrlCommand(uri.ToString(), $"Open {uri.Host}"));
        }

        if (DomainLike.IsMatch(target))
        {
            var url = "https://" + target;
            return ($"Opening {url}...", UrlCommand(url, $"Open {target}"));
        }

        var alias = target.ToLowerInvariant();
        if (KnownApplications.TryGet(alias, out var app))
        {
            return ($"Launching {app.DisplayName}...", new DesktopCommand
            {
                Kind = CommandKind.OpenApplication,
                Argument = app.Alias,
                Explanation = $"Launch {app.DisplayName}",
                Safety = CommandSafety.Safe
            });
        }

        if (KnownApplications.IsKnownSite(alias))
        {
            var url = KnownApplications.SiteUrl(alias)!;
            return ($"Opening {url}...", UrlCommand(url, $"Open {alias}"));
        }

        if (PathLike.IsMatch(target))
        {
            var path = ExpandPath(target);
            return ($"Opening {path}...", new DesktopCommand
            {
                Kind = CommandKind.OpenPath,
                Argument = path,
                Explanation = $"Open {path}",
                Safety = CommandSafety.NeedsConfirmation
            });
        }

        return ($"I don't know how to open \"{target}\". Try a website (github.com), a known app (notepad, chrome) or a folder path.", null);
    }

    private static DesktopCommand UrlCommand(string url, string explanation) => new()
    {
        Kind = CommandKind.OpenUrl,
        Argument = url,
        Explanation = explanation,
        Safety = CommandSafety.Safe
    };

    private static string ExpandPath(string path)
    {
        if (path == "~" || path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }

        return Path.GetFullPath(path);
    }

    private const string Capabilities =
        "Nexi can: open websites (open github.com), launch apps (open notepad, open chrome), " +
        "open files and folders (open ~/Documents), search the web (search cute cats), " +
        "show the time and system info, and run scripts after your confirmation.";
}
