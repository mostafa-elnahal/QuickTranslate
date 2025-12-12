# QuickTranslate - WPF Translation Utility

A lightweight, fast, and responsive .NET 8.0 WPF application that provides instant translation of selected text via a global hotkey (Ctrl+Shift+T). The application acts as a spiritual successor to QTranslate.

## Features

✅ **Global Hotkey Support** - Press Ctrl+Shift+T to translate selected text  
✅ **Multi-Provider Support** - Google, Bing, Yandex, Microsoft via GTranslate  
✅ **System Tray Integration** - Runs minimized in the system tray  
✅ **Non-Intrusive UI** - Borderless, transparent window that doesn't steal focus  
✅ **Smart Positioning** - Window appears near mouse cursor  
✅ **Auto-Hide** - Window automatically hides after 20 seconds or on click  
✅ **Dark Theme** - Modern dark UI with rounded corners  

## How to Use

1. **Start the Application**
   ```bash
   dotnet run
   ```
   The application will minimize to the system tray with a 'T' icon.

2. **Translate Text**
   - Select any text in any application
   - Press **Ctrl + Shift + T**
   - The translation window will appear near your cursor
   - The text is translated using the selected provider (Default: Google)

3. **Switch Providers**
   - Click the provider name at the bottom of the window (Google, Bing, etc.)
   - The translation will re-run with the new provider

4. **Hide the Window**
   - Click anywhere on the translation window
   - Or wait 20 seconds for auto-hide

5. **Exit the Application**
   - Right-click the tray icon
   - Select "Exit"

## Project Structure

```
QuickTranslate/
├── QuickTranslate.csproj      # .NET 8.0 WPF project file
├── App.xaml                   # Application resources
├── App.xaml.cs                # System tray & hotkey registration
├── MainWindow.xaml            # Borderless transparent UI
├── MainWindow.xaml.cs         # Window positioning & lifecycle
├── MainViewModel.cs           # MVVM ViewModel
├── NativeMethods.cs           # P/Invoke wrapper for Win32 APIs
├── Services/
│   ├── GTranslateService.cs   # GTranslate wrapper implementation
│   ├── ITranslationService.cs # Translation service interface
│   └── TranslatorFactory.cs   # Lazy loading of translators
└── Converters/                # Value converters (BoolToFlowDirection, etc.)
```

## Technical Details

### Architecture

- **MVVM Pattern**: Clean separation between UI and business logic.
- **Service Layer**: `ITranslationService` abstraction allows easy swapping of translation backends.
- **Dependency Injection**: Services are injected into the ViewModel.
- **P/Invoke**: Direct Win32 API calls for hotkey registration (`RegisterHotKey`) and keyboard simulation (`SendInput`).

### Key Components

#### Services/GTranslateService.cs
- Implements `ITranslationService` using the **GTranslate** library.
- Supports multiple providers: Google, Bing, Yandex, Microsoft.
- Handles lazy loading of translators to optimize memory.

#### MainViewModel.cs
- Manages UI state (`IsLoading`, `CurrentTranslation`).
- Orchestrates the translation flow: `TranslateClipboardAsync()`.

#### NativeMethods.cs
- `RegisterHotKey` / `UnregisterHotKey` - Global hotkey management.
- `SendInput` - Keyboard input simulation (used to copy text).
- `GetCursorPos` - Positions the window near the mouse.

### Workflow

1. User presses **Ctrl + Shift + T**.
2. `WndProc` receives `WM_HOTKEY` message.
3. `SimulateCopyKeystroke()` sends `Ctrl+C` to the foreground app.
4. (Small ~100ms delay ensures clipboard is populated).
5. ViewModel reads clipboard content.
6. `GTranslateService` fetches the real translation.
7. Result is displayed in the popup window.

## Build & Run

### Prerequisites
- .NET 8.0 SDK or later
- Windows OS (uses Win32 APIs)

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

### Publish (Optional)
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Future Enhancements

- [ ] Add language detection
- [ ] Configurable hotkeys
- [ ] Translation history
- [ ] Text-to-speech for translations
- [ ] Settings menu

## Notes

- `ShowActivated="False"` prevents the window from stealing focus but also means it won't receive keyboard input.
- Global hotkeys may conflict with other applications using the same combination.

## License

This is a demonstration project created for educational purposes.
