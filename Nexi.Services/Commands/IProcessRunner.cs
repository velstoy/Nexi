using System.Diagnostics;

namespace Nexi.Services.Commands;

/// <summary>Abstraction over launching OS processes, so the executor can be unit-tested.</summary>
public interface IProcessRunner
{
    void Start(string fileName, string arguments, bool useShellExecute = true);
}

public sealed class ProcessRunner : IProcessRunner
{
    public void Start(string fileName, string arguments, bool useShellExecute = true)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = useShellExecute
        });
    }
}
