# Nexi Desktop Assistant - Technical Overview

## Architecture
A desktop application built with .NET 10 using Avalonia UI 12 and MVVM (CommunityToolkit.Mvvm), with
dependency injection via Microsoft.Extensions.DependencyInjection. The app takes voice or text
commands, interprets them with a pluggable AI provider, and executes validated desktop control
commands.

## Core Components

### Voice Processing
- NAudio for microphone capture (Windows)
- Whisper.net for local, offline speech-to-text (tiny model downloaded on first use)
- Voice is optional; the app is fully usable with typed input

### AI Integration
All providers implement `IAssistantProvider` and are selected in the app header:
- **Local assistant** - built-in rule-based provider, always available, no configuration needed
- **Ollama** - local models via OllamaSharp (implements `Microsoft.Extensions.AI.IChatClient`), default endpoint http://localhost:11434
- **OpenAI-compatible cloud** - OpenAI, Azure OpenAI, Groq, OpenRouter, etc. via `Microsoft.Extensions.AI.OpenAI` with an API key and custom base URL

The model is instructed (system prompt) to append a JSON block for desktop actions, which
`DesktopCommandParser` extracts and `CommandSafetyClassifier` validates.

### Desktop Control
- `DesktopExecutor` runs commands: open URL, launch known app, open file/folder, run script,
  get system info, get time, show message
- Safety policy: `Safe` commands run immediately, `NeedsConfirmation` commands show a confirm
  banner in the UI, `Blocked` commands (destructive scripts, non-http URLs, ...) are refused
- Process launches go through `IProcessRunner` (mockable in tests)

### Persistence
- Nexi.Data: EF Core 10 + SQLite (`%AppData%/Nexi/nexi.db`) storing chat messages, command
  history and settings via `INexiStore`

## Data Flow
1. Voice input captured through NAudio (or typed input)
2. Speech converted to text via Whisper.net (local)
3. Text processed by the selected AI provider
4. Response parsed for a structured desktop command
5. Command safety-classified, then executed through OS APIs (after confirmation when needed)
6. UI updated with the reply and outcome; everything persisted to SQLite

## UI
- Avalonia 12, dark theme with purple AI accents (see `App.axaml`)
- Chat-style conversation view, provider/model selector, voice toggle, confirmation banner

## Development Requirements

### Tools & SDKs
- .NET 10 SDK
- Visual Studio 2022 (17.14+) or JetBrains Rider

### Key Packages
- Avalonia / Avalonia.Desktop / Avalonia.Themes.Fluent / Avalonia.Fonts.Inter 12.x
- CommunityToolkit.Mvvm
- Microsoft.Extensions.AI (+ .OpenAI), OllamaSharp
- NAudio, Whisper.net + Whisper.net.Runtime
- Microsoft.EntityFrameworkCore.Sqlite

### Dependencies
- Local AI models (optional - Ollama) or an API key (optional - cloud providers)
- Without any of those, the built-in local assistant keeps the app fully functional

## Setup Instructions
1. Install .NET 10 SDK
2. Clone repository
3. `dotnet restore` / `dotnet build Nexi.sln`
4. Run `Nexi.UI.Desktop` (produces `Nexi.exe`)
5. `dotnet test Nexi.sln` runs the unit + headless UI tests

## Configuration
- The Settings page sets the AI provider, model, API key, endpoint, microphone, speech model and theme; persisted in `AppSettings`
- The Models page installs and manages models in-app: pull/delete/select Ollama models, search and download Hugging Face GGUF files (with progress and cancel, importable into Ollama), and download Whisper speech-recognition models
- Command permissions (`AutoConfirmSafeCommands`, history limit) are in `AppSettings`
