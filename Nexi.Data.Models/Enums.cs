namespace Nexi.Data.Models;

public enum ChatRole
{
    System,
    User,
    Assistant
}

public enum AiProviderKind
{
    LocalAssistant,
    Ollama,
    OpenAICompatible
}

public enum CommandKind
{
    None,
    OpenUrl,
    OpenApplication,
    OpenPath,
    RunScript,
    GetSystemInfo,
    GetTime,
    ShowMessage
}

public enum CommandSafety
{
    Safe,
    NeedsConfirmation,
    Blocked
}
