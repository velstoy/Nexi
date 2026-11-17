using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

using Nexi.Data.Models;

namespace Nexi.Services.Commands;

/// <summary>
/// Executes validated desktop commands: opens URLs, apps and paths, gathers system info,
/// and runs scripts only after explicit confirmation.
/// </summary>
public sealed class DesktopExecutor : IDesktopCommandExecutor
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<DesktopExecutor> _logger;

    public DesktopExecutor(IProcessRunner processRunner, ILogger<DesktopExecutor> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public Task<CommandExecutionResult> ExecuteAsync(DesktopCommand command, bool confirmed, CancellationToken ct = default)
    {
        if (command.Safety == CommandSafety.Blocked)
        {
            _logger.LogWarning("Blocked command {Kind}: {Argument}", command.Kind, command.Argument);
            return Task.FromResult(CommandExecutionResult.Fail(
                $"Blocked by safety policy: {command.Explanation ?? command.Kind.ToString()}"));
        }

        if (command.Safety == CommandSafety.NeedsConfirmation && !confirmed)
        {
            return Task.FromResult(CommandExecutionResult.Fail("This command requires user confirmation."));
        }

        try
        {
            var result = command.Kind switch
            {
                CommandKind.OpenUrl => OpenUrl(command.Argument),
                CommandKind.OpenApplication => OpenApplication(command.Argument),
                CommandKind.OpenPath => OpenPath(command.Argument),
                CommandKind.RunScript => RunScript(command.Argument),
                CommandKind.GetSystemInfo => CommandExecutionResult.Ok(GetSystemInfo()),
                CommandKind.GetTime => CommandExecutionResult.Ok(DateTime.Now.ToString("F")),
                CommandKind.ShowMessage => CommandExecutionResult.Ok(command.Argument),
                _ => CommandExecutionResult.Fail($"Unsupported command kind: {command.Kind}")
            };

            _logger.LogInformation("Executed {Kind} ({Argument}): success={Success}", command.Kind, command.Argument, result.Success);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute {Kind}: {Argument}", command.Kind, command.Argument);
            return Task.FromResult(CommandExecutionResult.Fail(ex.Message));
        }
    }

    private CommandExecutionResult OpenUrl(string url)
    {
        if (!CommandSafetyClassifier.IsSafeHttpUrl(url))
        {
            return CommandExecutionResult.Fail($"Refusing to open non-http(s) URL: {url}");
        }

        _processRunner.Start(url, string.Empty);
        return CommandExecutionResult.Ok($"Opened {url} in the default browser.");
    }

    private CommandExecutionResult OpenApplication(string alias)
    {
        if (!KnownApplications.TryGet(alias, out var app))
        {
            return CommandExecutionResult.Fail($"Unknown application: {alias}");
        }

        if (OperatingSystem.IsWindows())
        {
            _processRunner.Start(app.WindowsExe, string.Empty);
        }
        else if (OperatingSystem.IsMacOS())
        {
            _processRunner.Start("open", $"-a \"{app.MacApp}\"");
        }
        else
        {
            _processRunner.Start(app.LinuxCommand, string.Empty);
        }

        return CommandExecutionResult.Ok($"Launched {app.DisplayName}.");
    }

    private CommandExecutionResult OpenPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            return CommandExecutionResult.Fail($"Path does not exist: {fullPath}");
        }

        if (OperatingSystem.IsWindows())
        {
            _processRunner.Start("explorer.exe", $"\"{fullPath}\"");
        }
        else if (OperatingSystem.IsMacOS())
        {
            _processRunner.Start("open", $"\"{fullPath}\"");
        }
        else
        {
            _processRunner.Start("xdg-open", $"\"{fullPath}\"");
        }

        return CommandExecutionResult.Ok($"Opened {fullPath}.");
    }

    private CommandExecutionResult RunScript(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return CommandExecutionResult.Fail("Empty script.");
        }

        if (OperatingSystem.IsWindows())
        {
            _processRunner.Start("cmd.exe", $"/c \"{script}\"");
        }
        else
        {
            _processRunner.Start("/bin/sh", $"-c \"{script}\"");
        }

        return CommandExecutionResult.Ok($"Ran: {script}");
    }

    private static string GetSystemInfo()
    {
        var lines = new List<string>
        {
            $"OS: {RuntimeInformation.OSDescription}",
            $"Architecture: {RuntimeInformation.OSArchitecture}",
            $"Machine: {Environment.MachineName}",
            $"Processors: {Environment.ProcessorCount}",
            $"64-bit process: {Environment.Is64BitProcess}",
            $"CLR: {RuntimeInformation.FrameworkDescription}",
            $"Uptime: {TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\.hh\:mm\:ss")}"
        };

        if (OperatingSystem.IsWindows() && TryGetPhysicalMemory(out var totalGb, out var availableGb))
        {
            lines.Add($"RAM: {availableGb:F1} GB available of {totalGb:F1} GB");
        }
        else
        {
            lines.Add($"Managed memory in use: {GC.GetTotalMemory(false) / 1024d / 1024d:F1} MB");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryGetPhysicalMemory(out double totalGb, out double availableGb)
    {
        totalGb = availableGb = 0;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref status))
            {
                return false;
            }

            totalGb = status.ullTotalPhys / 1024d / 1024d / 1024d;
            availableGb = status.ullAvailPhys / 1024d / 1024d / 1024d;
            return true;
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
