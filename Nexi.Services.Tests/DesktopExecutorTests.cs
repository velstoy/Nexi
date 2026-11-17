using Microsoft.Extensions.Logging.Abstractions;

using Nexi.Data.Models;
using Nexi.Services.Commands;

namespace Nexi.Services.Tests;

public class DesktopExecutorTests
{
    private readonly FakeProcessRunner _runner = new();
    private readonly DesktopExecutor _executor;

    public DesktopExecutorTests()
    {
        _executor = new DesktopExecutor(_runner, NullLogger<DesktopExecutor>.Instance);
    }

    [Fact]
    public async Task Safe_url_command_opens_browser()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.OpenUrl,
            Argument = "https://example.com",
            Safety = CommandSafety.Safe
        };

        var result = await _executor.ExecuteAsync(command, confirmed: false);

        Assert.True(result.Success);
        var launched = Assert.Single(_runner.Started);
        Assert.Equal("https://example.com", launched.FileName);
    }

    [Fact]
    public async Task Non_http_url_is_refused_even_when_safe_marked()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.OpenUrl,
            Argument = "file:///etc/passwd",
            Safety = CommandSafety.Safe
        };

        var result = await _executor.ExecuteAsync(command, confirmed: true);

        Assert.False(result.Success);
        Assert.Empty(_runner.Started);
    }

    [Fact]
    public async Task NeedsConfirmation_command_does_not_run_without_confirmation()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.RunScript,
            Argument = "echo hi",
            Safety = CommandSafety.NeedsConfirmation
        };

        var result = await _executor.ExecuteAsync(command, confirmed: false);

        Assert.False(result.Executed);
        Assert.Contains("confirmation", result.Error);
        Assert.Empty(_runner.Started);
    }

    [Fact]
    public async Task NeedsConfirmation_command_runs_when_confirmed()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.RunScript,
            Argument = "echo hi",
            Safety = CommandSafety.NeedsConfirmation
        };

        var result = await _executor.ExecuteAsync(command, confirmed: true);

        Assert.True(result.Success);
        var launched = Assert.Single(_runner.Started);
        Assert.Contains("echo hi", launched.Arguments);
    }

    [Fact]
    public async Task Blocked_command_is_always_refused()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.RunScript,
            Argument = "rm -rf /",
            Safety = CommandSafety.Blocked
        };

        var result = await _executor.ExecuteAsync(command, confirmed: true);

        Assert.False(result.Executed);
        Assert.Contains("Blocked", result.Error);
        Assert.Empty(_runner.Started);
    }

    [Fact]
    public async Task Known_application_is_launched()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.OpenApplication,
            Argument = "notepad",
            Safety = CommandSafety.Safe
        };

        var result = await _executor.ExecuteAsync(command, confirmed: false);

        Assert.True(result.Success);
        Assert.Single(_runner.Started);
    }

    [Fact]
    public async Task Missing_path_reports_error()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.OpenPath,
            Argument = Path.Combine(Path.GetTempPath(), "nexi-definitely-missing-" + Guid.NewGuid()),
            Safety = CommandSafety.Safe
        };

        var result = await _executor.ExecuteAsync(command, confirmed: true);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.Error);
        Assert.Empty(_runner.Started);
    }

    [Fact]
    public async Task Existing_path_is_opened()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.OpenPath,
            Argument = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Safety = CommandSafety.Safe
        };

        var result = await _executor.ExecuteAsync(command, confirmed: true);

        Assert.True(result.Success);
        Assert.Single(_runner.Started);
    }

    [Fact]
    public async Task System_info_returns_details()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.GetSystemInfo,
            Safety = CommandSafety.Safe
        };

        var result = await _executor.ExecuteAsync(command, confirmed: false);

        Assert.True(result.Success);
        Assert.Contains("OS:", result.Output);
        Assert.Contains("Processors:", result.Output);
    }

    [Fact]
    public async Task ShowMessage_echoes_argument()
    {
        var command = new DesktopCommand
        {
            Kind = CommandKind.ShowMessage,
            Argument = "Hello from Nexi",
            Safety = CommandSafety.Safe
        };

        var result = await _executor.ExecuteAsync(command, confirmed: false);

        Assert.True(result.Success);
        Assert.Equal("Hello from Nexi", result.Output);
    }
}
