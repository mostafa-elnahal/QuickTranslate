using System;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslate.Services;
using QuickTranslate.ViewModels;
using QuickTranslate.Views;
using QuickTranslate.Services.Providers;
using System.Collections.Generic;

namespace QuickTranslate;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // Translation popup
    private TranslationPopup? _translationPopup;
    private PopupViewModel? _translationViewModel;

    // Pronunciation popup
    private PronunciationPopup? _pronunciationPopup;
    private PronunciationViewModel? _pronunciationViewModel;

    // Services
    private IClipboardService? _clipboardService;
    private IHotkeyService? _hotkeyService;
    private ITrayIconService? _trayIconService;
    private IWindowPositioningService? _positioningService;
    private IWindowSizingService? _sizingService;
    private ISettingsService? _settingsService;
    private ITranslationService? _translationService;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        string errorMessage = $"Exception detected: {e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}";
        if (e.Exception.InnerException != null)
        {
            errorMessage += $"\n\nInner Exception: {e.Exception.InnerException.Message}";
        }

        System.IO.File.WriteAllText("crash_log.txt", errorMessage);
        MessageBox.Show($"Application Crashed. Log saved to crash_log.txt.\n{e.Exception.Message}", "QuickTranslate Error");
        e.Handled = true; // Prevent app from closing immediately
        Shutdown();
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Initialize Services
        _settingsService = new SettingsService();
        _hotkeyService = new HotkeyService();
        _clipboardService = new ClipboardService();
        _trayIconService = new TrayIconService();
        _positioningService = new WindowPositioningService();
        _sizingService = new WindowSizingService(_settingsService);
        _translationService = new GTranslateService();
        IDialogService dialogService = new DialogService();

        // Create syllable service for pronunciation
        ISyllableService syllableService = new SyllableService();

        // Register Providers
        var providers = new List<IPronunciationProvider>
        {
            new GooglePronunciationProvider(_translationService, syllableService),
            new GeminiPronunciationProvider(_translationService, syllableService, _settingsService)
        };

        IPronunciationService pronunciationService = new PronunciationService(providers, _settingsService);

        // Initialize Translation Popup (Composition Root)
        _translationViewModel = new PopupViewModel(_translationService, _settingsService, pronunciationService, _clipboardService);
        _translationPopup = new TranslationPopup(_translationViewModel, _positioningService, _sizingService);

        // Initialize Pronunciation Popup
        _pronunciationViewModel = new PronunciationViewModel(pronunciationService, _settingsService);
        _pronunciationPopup = new PronunciationPopup(_pronunciationViewModel, _positioningService, _sizingService);

        // Setup system tray icon
        SetupTrayIcon();

        // Register global hotkeys
        RegisterGlobalHotkeys();

        // Listen for setting changes
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_settingsService != null && _hotkeyService != null && _translationPopup != null)
        {
            RegisterTranslationHotkey(_settingsService.Settings.Hotkey);
            RegisterPronunciationHotkey(_settingsService.Settings?.PronunciationHotkey ?? "Ctrl+Shift+P");
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        // Cleanup
        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        _translationViewModel?.Dispose();
        _pronunciationViewModel?.Dispose();
        if (_translationService is IDisposable disposableService)
        {
            disposableService.Dispose();
        }
    }

    private void SetupTrayIcon()
    {
        if (_trayIconService == null) return;

        _trayIconService.Initialize();

        _trayIconService.ExitRequested += (s, args) => Shutdown();
        _trayIconService.SettingsRequested += (s, args) => OpenSettingsWindow();
    }

    private SettingsWindow? _settingsWindow;

    private void OpenSettingsWindow()
    {
        if (_settingsService == null || _translationService == null) return;

        // If window is already open, just focus it
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }
            return;
        }

        var viewModel = new SettingsViewModel(_settingsService, new DialogService(), _translationService);
        _settingsWindow = new SettingsWindow(viewModel);

        // Handle closure to clear reference
        _settingsWindow.Closed += (s, args) => _settingsWindow = null;

        _settingsWindow.Show();
    }

    #region Hotkey Registration

    private const int HOTKEY_ID_TRANSLATE = 1;
    private const int HOTKEY_ID_PRONUNCIATION = 2;

    private void RegisterGlobalHotkeys()
    {
        if (_hotkeyService == null) return;

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Register Translation hotkey (Ctrl+Q by default)
        if (_settingsService?.Settings.Hotkey != null && _translationPopup != null)
        {
            RegisterTranslationHotkey(_settingsService.Settings.Hotkey);
        }

        // Register Pronunciation hotkey from settings
        if (_pronunciationPopup != null && _settingsService != null)
        {
            RegisterPronunciationHotkey(_settingsService.Settings?.PronunciationHotkey ?? "Ctrl+Shift+P");
        }
    }

    private void RegisterTranslationHotkey(string hotkey)
    {
        if (_hotkeyService == null || _translationPopup == null) return;

        bool success = _hotkeyService.Register(HOTKEY_ID_TRANSLATE, hotkey, _translationPopup);
        if (!success)
        {
            MessageBox.Show(
                $"Failed to register translation hotkey '{hotkey}'. It may be in use by another application.",
                "QuickTranslate",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RegisterPronunciationHotkey(string hotkey)
    {
        if (_hotkeyService == null || _pronunciationPopup == null) return;

        bool success = _hotkeyService.Register(HOTKEY_ID_PRONUNCIATION, hotkey, _pronunciationPopup);
        if (!success)
        {
            MessageBox.Show(
                $"Failed to register pronunciation hotkey '{hotkey}'. It may be in use by another application.",
                "QuickTranslate",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    #endregion

    /// <summary>
    /// Handles the hotkey press event
    /// </summary>
    private async void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (_clipboardService == null) return;

        // Run capture on STA thread (required for clipboard/SendKeys)
        string capturedText = string.Empty;

        try
        {
            var tcs = new TaskCompletionSource<string>();
            var thread = new System.Threading.Thread(() =>
            {
                try { tcs.SetResult(_clipboardService.CaptureSelection()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            
            capturedText = await tcs.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard capture exception: {ex}");
            return;
        }

        switch (hotkeyId)
        {
            case HOTKEY_ID_TRANSLATE:
                // Close pronunciation popup if open
                _pronunciationViewModel?.HideWindow();
                // Show translation popup
                _translationPopup?.ShowAndTranslate(capturedText);
                break;

            case HOTKEY_ID_PRONUNCIATION:
                // Close translation popup if open
                _translationViewModel?.HideWindow();

                // Show pronunciation popup only if text is not empty
                if (!string.IsNullOrWhiteSpace(capturedText))
                {
                    _pronunciationPopup?.ShowAndPronounce(capturedText);
                }
                break;
        }
    }
}
