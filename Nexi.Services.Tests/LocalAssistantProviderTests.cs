using Microsoft.Extensions.Logging.Abstractions;

using Nexi.Data.Models;
using Nexi.Services.AI;

namespace Nexi.Services.Tests;

public class LocalAssistantProviderTests
{
    private readonly LocalAssistantProvider _provider = new(NullLogger<LocalAssistantProvider>.Instance);

    private async Task<AssistantReply> ReplyTo(string input)
    {
        var conversation = new List<ChatMessage>
        {
            new() { Role = ChatRole.User, Content = input }
        };
        return await _provider.ReplyAsync(conversation, new AppSettings());
    }

    [Fact]
    public async Task Greeting_is_conversational_without_command()
    {
        var reply = await ReplyTo("hello there");
        Assert.Null(reply.Command);
        Assert.Contains("Nexi", reply.Text);
    }

    [Fact]
    public async Task Time_query_returns_GetTime_command()
    {
        var reply = await ReplyTo("what time is it?");
        Assert.NotNull(reply.Command);
        Assert.Equal(CommandKind.GetTime, reply.Command!.Kind);
        Assert.Equal(CommandSafety.Safe, reply.Command.Safety);
    }

    [Fact]
    public async Task Open_full_url_produces_safe_OpenUrl()
    {
        var reply = await ReplyTo("open https://example.com/page");
        Assert.Equal(CommandKind.OpenUrl, reply.Command!.Kind);
        Assert.Equal("https://example.com/page", reply.Command.Argument);
        Assert.Equal(CommandSafety.Safe, reply.Command.Safety);
    }

    [Fact]
    public async Task Open_bare_domain_gets_https_prefix()
    {
        var reply = await ReplyTo("open github.com");
        Assert.Equal(CommandKind.OpenUrl, reply.Command!.Kind);
        Assert.Equal("https://github.com", reply.Command.Argument);
    }

    [Fact]
    public async Task Open_known_site_alias_maps_to_url()
    {
        var reply = await ReplyTo("open youtube");
        Assert.Equal(CommandKind.OpenUrl, reply.Command!.Kind);
        Assert.Equal("https://www.youtube.com", reply.Command.Argument);
    }

    [Fact]
    public async Task Search_opens_duckduckgo()
    {
        var reply = await ReplyTo("search cute cats");
        Assert.Equal(CommandKind.OpenUrl, reply.Command!.Kind);
        Assert.Contains("duckduckgo.com/?q=cute%20cats", reply.Command.Argument);
    }

    [Fact]
    public async Task Open_known_app_produces_safe_OpenApplication()
    {
        var reply = await ReplyTo("open notepad");
        Assert.Equal(CommandKind.OpenApplication, reply.Command!.Kind);
        Assert.Equal("notepad", reply.Command.Argument);
        Assert.Equal(CommandSafety.Safe, reply.Command.Safety);
    }

    [Fact]
    public async Task Open_folder_path_produces_OpenPath()
    {
        var reply = await ReplyTo("open ~/Documents");
        Assert.Equal(CommandKind.OpenPath, reply.Command!.Kind);
        Assert.EndsWith("Documents", reply.Command.Argument);
    }

    [Fact]
    public async Task System_info_query_returns_GetSystemInfo()
    {
        var reply = await ReplyTo("give me the system info");
        Assert.Equal(CommandKind.GetSystemInfo, reply.Command!.Kind);
        Assert.Equal(CommandSafety.Safe, reply.Command.Safety);
    }

    [Fact]
    public async Task Dangerous_script_is_blocked()
    {
        var reply = await ReplyTo("run rm -rf /");
        Assert.Equal(CommandKind.RunScript, reply.Command!.Kind);
        Assert.Equal(CommandSafety.Blocked, reply.Command.Safety);
        Assert.Contains("can't", reply.Text);
    }

    [Fact]
    public async Task Benign_script_needs_confirmation()
    {
        var reply = await ReplyTo("run echo hello");
        Assert.Equal(CommandKind.RunScript, reply.Command!.Kind);
        Assert.Equal(CommandSafety.NeedsConfirmation, reply.Command.Safety);
    }

    [Fact]
    public async Task Unknown_input_falls_back_to_helpful_text()
    {
        var reply = await ReplyTo("blorp the flibberty");
        Assert.Null(reply.Command);
        Assert.Contains("open", reply.Text);
    }
}
