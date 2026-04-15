# Project Context Map

> **For AI Agents:** Read this file first to understand the project architecture, critical patterns, and entry points.

## 1. Project Identity

* **Name**: QuickTranslate
* **Type**: WPF Application (.NET 8.0)
* **Core Function**: Global hotkey translator utility (System Tray app).
* **Key Philosophy**: Non-intrusive, fast, lightweight, "spiritual successor to QTranslate".

## 2. Architecture Overview

The application follows the **MVVM (Model-View-ViewModel)** pattern with a heavy reliance on **Dependency Injection (DI)** configured in `App.xaml.cs`.

### Composition Root

* **File**: `App.xaml.cs` (`Application_Startup`)
* **Role**: Initializes all services, ViewModels, and Windows. Registers global hotkeys and tray icons.

### Key Components Map

| Feature                 | View (UI)                   | ViewModel (Logic)             | Key Services                                           |
| :---------------------- | :-------------------------- | :---------------------------- | :----------------------------------------------------- |
| **Translation**   | `TranslationPopup.xaml`   | `PopupViewModel.cs`         | `ITranslationService`, `IWindowPositioningService` |
| **Pronunciation** | `PronunciationPopup.xaml` | `PronunciationViewModel.cs` | `IPronunciationService`, `ISyllableService`        |
| **Settings**      | `SettingsWindow.xaml`     | `SettingsViewModel.cs`      | `ISettingsService`                                   |
| **Tray Icon**     | N/A (System Resource)       | N/A                           | `ITrayIconService`                                   |

## 3. Critical Code Patterns

**⚠️ DO NOT BREAK THESE PATTERNS**

### A. Async Guard (Generation Counter)

* **Problem**: Rapid hotkey presses can trigger multiple async translation requests. If an old request finishes *after* a new one, it overwrites the UI with stale data.
* **Solution**: ViewModels maintain an `int _generation` counter.
  * Increment on every new request.
  * Capture local `myGeneration` before async call.
  * After await, check `if (myGeneration != _generation) return;`.
* **Used In**: `PopupViewModel.cs`, `PronunciationViewModel.cs`.

### B. STA Thread Capture

* **Problem**: Clipboard access and `SendKeys` (to copy text) often fail on background threads or require STA mode.
* **Solution**: Input capture is forced onto a dedicated STA thread.
* **File**: `App.xaml.cs` -> `OnHotkeyPressed` -> `Thread.SetApartmentState(ApartmentState.STA)`.

### C. Window Positioning Strategy

* **Problem**: Windows appearing off-screen or far from cursor due to DPI scaling issues on first launch.
* **Solution**:
  1. **DPI**: Use `VisualTreeHelper.GetDpi(window)` (fallback to System DPI) instead of `PresentationSource` (which is null on first show).
  2. **Sizing**: Window size must be calculated *before* positioning. logic: `Measure`/`Arrange` or forced `UpdateLayout` (with Visibility=Visible) is required before `PositionNearCursor`.

## 4. Service Registry

| Interface                     | Implementation               | Purpose                                          |
| :---------------------------- | :--------------------------- | :----------------------------------------------- |
| `ITranslationService`       | `GTranslateService`        | Wraps `GTranslate` lib (Google, Bing, Yandex). |
| `IPronunciationService`     | `PronunciationService`     | Facade for `Google` & `Gemini` providers.        |
| `IWindowPositioningService` | `WindowPositioningService` | Smart positioning logic (cursor-relative).       |
| `IWindowSizingService`      | `WindowSizingService`      | Persists/restores window dimensions.             |
| `ISyllableService`          | `SyllableService`          | Rule-based phonetic splitter for "karaoke" UI.   |
| `IStreamingAudioPlayer`     | `NAudioStreamingPlayer`    | Handles streaming audio chunks + PCM playback.   |
| `IHotkeyService`            | `HotkeyService`            | P/Invoke wrapper for global `RegisterHotKey`.  |

## 5. File System Layout

* `/Views`: all `.xaml` windows.
* `/ViewModels`: all MVVM logic.
* `/Services`: Business logic and external APIs.
* `/Models`: Data structures (`TranslationModel`, `AppSettings`).
* `/NativeMethods.txt` (or `.cs` via CsWin32): P/Invoke definitions.
