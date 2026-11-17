namespace Nexi.Services.Commands;

/// <summary>Outcome of attempting to execute a desktop command.</summary>
public sealed class CommandExecutionResult
{
    public bool Executed { get; init; }

    public bool Success { get; init; }

    public string? Output { get; init; }

    public string? Error { get; init; }

    public static CommandExecutionResult Ok(string? output = null) =>
        new() { Executed = true, Success = true, Output = output };

    public static CommandExecutionResult Fail(string error) =>
        new() { Executed = false, Success = false, Error = error };
}
