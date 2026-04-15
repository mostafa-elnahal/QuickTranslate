# QuickTranslate - WPF Translation Utility

A lightweight, fast, and responsive .NET 8.0 WPF application that provides instant translation and pronunciation of selected text via global hotkeys. The application acts as a spiritual successor to QTranslate.

## Features

✅ **Global Hotkey Support** - Press **Ctrl+Shift+T** to translate selected text
✅ **Pronunciation Support** - Press **Ctrl+Shift+P** for a dedicated pronunciation popup (Supports Google translate & Gemini AI)
✅ **Multi-Provider Support** - Google, Bing, Yandex, Microsoft via GTranslate
✅ **Gemini AI Integration** - High-quality, AI-powered speech synthesis for pronunciation
✅ **System Tray Integration** - Runs minimized in the system tray
✅ **Non-Intrusive UI** - Borderless, transparent, auto-positioning windows
✅ **Smart Sizing** - Windows remember their size and position intelligently
✅ **Modern Dark Theme** - Sleek UI with rounded corners and animations

## How to Use

1. **Start the Application**

   ```bash
   dotnet run
   ```

   The application will minimize to the system tray with a 'T' icon.
2. **Translate Text**

   - Select any text in any application
   - Press **Ctrl + Shift + T** (Configurable)
   - The translation window will appear near your cursor
3. **Check Pronunciation**

   - Select a word
   - Press **Ctrl + Shift + P** (Configurable)
   - A dedicated window will appear with syllable breakdown, phonetics, and audio playback.
4. **Settings**

   - Right-click the tray icon -> **Options**
   - Configure providers, hotkeys, fonts, and API keys.

## Project Structure

```
QuickTranslate/
├── QuickTranslate.csproj      # .NET 8.0 WPF project file
├── App.xaml.cs                # Composition Root & Startup Logic
├── Views/
│   ├── TranslationPopup.xaml  # Main Translation Window
│   ├── PronunciationPopup.xaml# Dedicated Pronunciation Window
│   └── SettingsWindow.xaml    # Options & Configuration
├── ViewModels/
│   ├── PopupViewModel.cs      # Translation Logic
│   ├── PronunciationViewModel.cs # Audio/Karaoke Logic
│   └── SettingsViewModel.cs   # Configuration Logic
├── Services/
│   ├── GTranslateService.cs   # Translation Backend
│   ├── PronunciationService.cs# Speech Backend (Facade)
│   ├── SyllableService.cs     # Phonetic Syllable Splitter
│   └── WindowPositioningService.cs # Smart Cursor Positioning
└── docs/
    ├── CONTEXT_MAP.md         # Architecture Overview for Developers/AI
    └── pronunciation_state.md # Deep dive into Pronunciation features
```

## Technical Details

### Architecture

- **MVVM Pattern**: Strict separation of concern. ViewModels handle all business logic.
- **Dependency Injection**: All services are registered in `App.xaml.cs` and injected into ViewModels.
- **P/Invoke**: Uses `CsWin32` (via `NativeMethods.txt`) for robust Win32 API interop (Hotkeys, Clipboard, Window Styles).

### Key Workflows

1. **User Action**: Hotkey pressed.
2. **Capture**: App switches thread to **STA**, simulates `Ctrl+C` to hijack clipboard.
3. **Guard**: Generation counters (`_generation++`) prevent race conditions from rapid requests.
4. **Service**:
   - `TranslationService`: Fetches text.
   - `PronunciationService`: Fetches audio (and caches it).
5. **Display**: Window is positioned *after* sizing calculations (using `VisualTreeHelper` for accurate DPI support).

## Build & Run

### Prerequisites

- .NET 8.0 SDK
- Windows OS (Win32 API dependency)

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run
```

## License

This is a demonstration project created for educational purposes.
