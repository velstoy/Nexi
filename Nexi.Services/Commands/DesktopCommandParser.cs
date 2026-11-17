using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Nexi.Data.Models;

namespace Nexi.Services.Commands;

/// <summary>
/// Extracts a <see cref="DesktopCommand"/> from an LLM reply. The model is asked to append a
/// JSON block such as {"kind": "OpenUrl", "argument": "https://..."}; this parser finds and
/// validates it. Returns null when the reply contains no command.
/// </summary>
public static partial class DesktopCommandParser
{
    public static DesktopCommand? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var candidate in JsonCandidates(text))
        {
            DesktopCommand? command = TryParseCandidate(candidate);
            if (command != null)
            {
                return command;
            }
        }

        return null;
    }

    private static IEnumerable<string> JsonCandidates(string text)
    {
        foreach (Match match in FencedBlockRegex().Matches(text))
        {
            yield return match.Groups[1].Value;
        }

        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace && text.Contains("\"kind\"", StringComparison.OrdinalIgnoreCase))
        {
            yield return text[firstBrace..(lastBrace + 1)];
        }
    }

    private static DesktopCommand? TryParseCandidate(string json)
    {
        CommandDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CommandDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (dto?.Kind is null || !Enum.TryParse<CommandKind>(dto.Kind, ignoreCase: true, out var kind))
        {
            return null;
        }

        if (kind == CommandKind.None)
        {
            return null;
        }

        return new DesktopCommand
        {
            Kind = kind,
            Argument = dto.Argument?.Trim() ?? string.Empty,
            Explanation = dto.Explanation,
            RawJson = json,
            Safety = CommandSafety.NeedsConfirmation
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class CommandDto
    {
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("argument")]
        public string? Argument { get; set; }

        [JsonPropertyName("explanation")]
        public string? Explanation { get; set; }
    }

    [GeneratedRegex(@"```(?:json)?\s*(\{.*?\})\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex FencedBlockRegex();
}
