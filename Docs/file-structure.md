# 1. UI Layer
- Nexi.UI - Avalonia control library: purple-accented dark theme (`App.axaml`), sidebar shell with chat / chat history / models / settings pages (`Views/MainView.axaml` + page views), view models (`ViewModels/`), DI composition root (`App.axaml.cs`)
- Nexi.UI.Desktop - the runnable desktop app (`Program.cs`, `app.manifest`, output assembly `Nexi.exe`)

# 2. Services Layer
- Nexi.Services - core application logic:
  - `AI/` - `IAssistantProvider` implementations (local rule-based, Ollama, OpenAI-compatible) + factory
  - `Commands/` - command parsing (`DesktopCommandParser`), safety classification (`CommandSafetyClassifier`), execution (`DesktopExecutor`, `IProcessRunner`)
  - `Pipeline/` - `INexiAssistant` orchestrating input -> AI -> command -> execution -> persistence
  - `Speech/` - `ISpeechRecognitionService` with a Whisper.net implementation (Windows) and a null fallback
  - `Models/` - `IModelManager` for installing/managing AI models: Ollama pull/delete/import, Hugging Face GGUF catalog search + download with progress/cancel, Whisper speech-model download

# 3. Data Layer
- Nexi.Data - EF Core SQLite context (`NexiDbContext`) and the `INexiStore` persistence facade (chat messages, command history, settings)
- Nexi.Data.Models - entity models: `ChatMessage`, `CommandHistoryEntry`, `AppSettings`, `DesktopCommand` + enums

# 4. Tests Layer
- Nexi.Services.Tests - unit tests for the local assistant, parser, classifier, executor, pipeline and store (EF InMemory)
- Nexi.UI.Tests - view-model tests plus headless Avalonia smoke tests that load the real views (`Avalonia.Headless`)

**All projects listed in this markdown are subject to change. These are just the core projects created in order to establish the basic project logic.**
