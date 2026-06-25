using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using QuickTranslate.Models;
using QuickTranslate.Services;
using QuickTranslate.Services.Input;
using QuickTranslate.ViewModels;
using QuickTranslate.Views;
using QuickTranslate.Services.Providers;
using QuickTranslate.Services.Pronunciation;
using QuickTranslate.Services.Audio;
using QuickTranslate.Helpers;

namespace QuickTranslate;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        CrashLogger.LogException(e.Exception);
        e.Handled = true; // Prevent app from closing immediately
        Shutdown();
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Start services
        var trayIconService = _serviceProvider.GetRequiredService<ITrayIconService>();
        trayIconService.Initialize();
        trayIconService.ExitRequested += (s, args) => Shutdown();
        trayIconService.SettingsRequested += (s, args) => OpenSettingsWindow();
        trayIconService.OcrRequested += async (s, args) => await HandleOcrRequestAsync();

        // Subscribe to floating toolbar events
        var toolbarVm = _serviceProvider.GetRequiredService<FloatingToolbarViewModel>();
        toolbarVm.TranslateRequested += OnToolbarTranslateRequested;
        toolbarVm.PronounceRequested += OnToolbarPronounceRequested;

        // Register hotkeys
        RegisterGlobalHotkeys();

        // Listen for setting changes
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        settingsService.SettingsChanged += OnSettingsChanged;

        // Initialize text selection monitor
        var selectionMonitor = _serviceProvider.GetRequiredService<ITextSelectionMonitorService>();
        selectionMonitor.TextSelected += OnTextSelected;
        if (settingsService.Settings.ShowSelectionToolbar)
        {
            selectionMonitor.Start();
        }

        // Initialize MainWindow
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Wire tray icon to show main window
        trayIconService.ShowMainWindowRequested += (s, args) =>
        {
            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.WindowState = WindowState.Normal;
        };

        // Keep toolbar behaving as a tooltip, close on focus loss
        var foregroundMonitor = _serviceProvider.GetRequiredService<IForegroundWindowMonitorService>();
        foregroundMonitor.Start();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IWindowPositioningService, WindowPositioningService>();
        services.AddSingleton<ITranslationService, GTranslateService>();
        services.AddSingleton<IDictionaryService, GoogleDictionaryService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISyllableService, SyllableService>();

        // OCR Services
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IOcrService, WindowsMediaOcrService>();

        // UI Automation
        services.AddSingleton<IUiAutomationService, UiAutomationService>();

        // Text Selection Monitor
        services.AddSingleton<IGlobalInputHookService, GlobalInputHookService>();
        services.AddSingleton<ITextSelectionMonitorService, TextSelectionMonitorService>();
        services.AddSingleton<IForegroundWindowMonitorService, ForegroundWindowMonitorService>();

        // Conditional Sizing Service (factory pattern for legacy support if needed, but here simple)
        services.AddSingleton<IWindowSizingService>(sp => 
            new WindowSizingService(sp.GetRequiredService<ISettingsService>()));

        // Pronunciation Services
        services.AddSingleton<ILanguageMetadataService, LanguageMetadataService>();
        services.AddSingleton<IAudioStreamingService, AudioStreamingService>();
        services.AddSingleton<IStreamingAudioPlayerFactory, StreamingAudioPlayerFactory>();

        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IWordHighlightService, WordHighlightService>();

        // Pronunciation Providers & Service
        services.AddSingleton<IPronunciationProvider, GooglePronunciationProvider>();
        
        services.AddSingleton<IPronunciationProvider>(sp => 
            new GeminiPronunciationProvider(
                sp.GetRequiredService<ISettingsService>()));

        services.AddSingleton<IPronunciationProvider>(sp =>
            new GcpPronunciationProvider(
                sp.GetRequiredService<ISettingsService>()));

        services.AddSingleton<IPronunciationProvider>(sp =>
            new ElevenLabsPronunciationProvider(
                sp.GetRequiredService<ISettingsService>()));

        services.AddSingleton<IPronunciationService>(sp => 
            new PronunciationService(
                sp.GetServices<IPronunciationProvider>(), 
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<ITranslationService>(),
                sp.GetRequiredService<ISyllableService>()));

        // ViewModels
        services.AddSingleton<PopupViewModel>();
        services.AddSingleton<PronunciationViewModel>();
        services.AddSingleton<FloatingToolbarViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        // Windows/Views
        services.AddSingleton<TranslationPopup>();
        services.AddSingleton<PronunciationPopup>();
        services.AddSingleton<FloatingToolbarWindow>();
        services.AddSingleton<MainWindow>();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_serviceProvider != null)
        {
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            RegisterTranslationHotkey(settingsService.Settings.Hotkey);
            RegisterPronunciationHotkey(settingsService.Settings?.PronunciationHotkey ?? "Ctrl+Shift+P");
            RegisterOcrHotkey(settingsService.Settings?.OcrHotkey ?? Constants.Defaults.OcrHotkey);

            // Update OCR language
            var ocrService = _serviceProvider.GetRequiredService<IOcrService>();
            ocrService.CurrentLanguageCode = settingsService.Settings?.OcrLanguage ?? Constants.Defaults.OcrLanguage;

            // Update Text Selection Monitor State
            var selectionMonitor = _serviceProvider.GetRequiredService<ITextSelectionMonitorService>();
            if (settingsService.Settings.ShowSelectionToolbar)
            {
                selectionMonitor.Start();
            }
            else
            {
                selectionMonitor.Stop();
            }
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        if (_serviceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }
    }

    private SettingsWindow? _settingsWindow;

    private void OpenSettingsWindow()
    {
        if (_serviceProvider == null) return;

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

        var viewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
        _settingsWindow = new SettingsWindow(viewModel);

        // Handle closure to clear reference
        _settingsWindow.Closed += (s, args) => _settingsWindow = null;

        _settingsWindow.Show();
    }

    #region Hotkey Registration

    private const int HOTKEY_ID_TRANSLATE = 1;
    private const int HOTKEY_ID_PRONUNCIATION = 2;
    private const int HOTKEY_ID_OCR = 3;

    private void RegisterGlobalHotkeys()
    {
        if (_serviceProvider == null) return;
        var hotkeyService = _serviceProvider.GetRequiredService<IHotkeyService>();
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();

        hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Register Translation hotkey
        RegisterTranslationHotkey(settingsService.Settings.Hotkey);

        // Register Pronunciation hotkey
        RegisterPronunciationHotkey(settingsService.Settings?.PronunciationHotkey ?? "Ctrl+Shift+P");

        // Register OCR hotkey
        RegisterOcrHotkey(settingsService.Settings?.OcrHotkey ?? Constants.Defaults.OcrHotkey);

        // Initialize OCR language
        var ocrService = _serviceProvider.GetRequiredService<IOcrService>();
        ocrService.CurrentLanguageCode = settingsService.Settings?.OcrLanguage ?? Constants.Defaults.OcrLanguage;
    }

    private void RegisterTranslationHotkey(string hotkey)
    {
        if (_serviceProvider == null) return;
        var hotkeyService = _serviceProvider.GetRequiredService<IHotkeyService>();
        var translationPopup = _serviceProvider.GetRequiredService<TranslationPopup>();

        bool success = hotkeyService.Register(HOTKEY_ID_TRANSLATE, hotkey, translationPopup);
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
        if (_serviceProvider == null) return;
        var hotkeyService = _serviceProvider.GetRequiredService<IHotkeyService>();
        var pronunciationPopup = _serviceProvider.GetRequiredService<PronunciationPopup>();

        bool success = hotkeyService.Register(HOTKEY_ID_PRONUNCIATION, hotkey, pronunciationPopup);
        if (!success)
        {
            MessageBox.Show(
                $"Failed to register pronunciation hotkey '{hotkey}'. It may be in use by another application.",
                "QuickTranslate",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RegisterOcrHotkey(string hotkey)
    {
        if (_serviceProvider == null) return;
        var hotkeyService = _serviceProvider.GetRequiredService<IHotkeyService>();
        var translationPopup = _serviceProvider.GetRequiredService<TranslationPopup>();

        bool success = hotkeyService.Register(HOTKEY_ID_OCR, hotkey, translationPopup);
        if (!success)
        {
            MessageBox.Show(
                $"Failed to register OCR hotkey '{hotkey}'. It may be in use by another application.",
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
        if (_serviceProvider == null) return;

        // OCR uses screen capture, not clipboard text selection.
        // Skip the clipboard pipeline entirely to avoid injecting
        // keystrokes (Ctrl+C) into the foreground application.
        if (hotkeyId == HOTKEY_ID_OCR)
        {
            await HandleOcrRequestAsync();
            return;
        }

        var clipboardService = _serviceProvider.GetRequiredService<IClipboardService>();
        string capturedText = await clipboardService.CaptureSelectionAsync();

        if (string.IsNullOrWhiteSpace(capturedText))
            return;

        var translationViewModel = _serviceProvider.GetRequiredService<PopupViewModel>();
        var pronunciationViewModel = _serviceProvider.GetRequiredService<PronunciationViewModel>();
        var translationPopup = _serviceProvider.GetRequiredService<TranslationPopup>();
        var pronunciationPopup = _serviceProvider.GetRequiredService<PronunciationPopup>();

        switch (hotkeyId)
        {
            case HOTKEY_ID_TRANSLATE:
                // Close pronunciation popup if open
                pronunciationViewModel.HideWindow();
                // Show translation popup
                translationPopup.ShowAndTranslate(capturedText);
                break;

            case HOTKEY_ID_PRONUNCIATION:
                // Close translation popup if open
                translationViewModel.HideWindow();

                // Show pronunciation popup only if text is not empty
                if (!string.IsNullOrWhiteSpace(capturedText))
                {
                    pronunciationPopup.ShowAndPronounce(capturedText);
                }
                break;
        }
    }

    /// <summary>
    /// Handles text selection detected by the global mouse hook.
    /// Spawns the floating toolbar under the cursor.
    /// </summary>
    private void OnTextSelected(string text)
    {
        if (_serviceProvider == null || string.IsNullOrWhiteSpace(text)) return;
        
        var toolbarWindow = _serviceProvider.GetRequiredService<FloatingToolbarWindow>();
        toolbarWindow.ShowToolbar(text, ToolbarDisplayMode.Selection);
    }

    /// <summary>
    /// Handles the Translate action from the floating toolbar.
    /// </summary>
    private void OnToolbarTranslateRequested(string text)
    {
        if (_serviceProvider == null) return;

        var pronunciationViewModel = _serviceProvider.GetRequiredService<PronunciationViewModel>();
        pronunciationViewModel.HideWindow();

        var translationPopup = _serviceProvider.GetRequiredService<TranslationPopup>();
        translationPopup.ShowAndTranslate(text);
    }

    /// <summary>
    /// Handles the Pronounce action from the floating toolbar.
    /// </summary>
    private void OnToolbarPronounceRequested(string text)
    {
        if (_serviceProvider == null || string.IsNullOrWhiteSpace(text)) return;

        var translationViewModel = _serviceProvider.GetRequiredService<PopupViewModel>();
        translationViewModel.HideWindow();

        var pronunciationPopup = _serviceProvider.GetRequiredService<PronunciationPopup>();
        pronunciationPopup.ShowAndPronounce(text);
    }

    /// <summary>
    /// Handles OCR request from hotkey or tray menu.
    /// Captures a screen region and shows the floating toolbar expanded
    /// below the selection with OCR language selection.
    /// The overlay stays visible until the toolbar is dismissed or an action is taken.
    /// </summary>
    private async Task HandleOcrRequestAsync()
    {
        if (_serviceProvider == null) return;

        var screenCaptureService = _serviceProvider.GetRequiredService<IScreenCaptureService>();
        var toolbarWindow = _serviceProvider.GetRequiredService<FloatingToolbarWindow>();
        var toolbarVm = _serviceProvider.GetRequiredService<FloatingToolbarViewModel>();

        try
        {
            await screenCaptureService.CaptureRegionAsync(
                onRegionCaptured: (captureResult, completeCapture) =>
                {
                    toolbarWindow.ShowToolbar(captureResult.Bitmap, captureResult.SelectionBounds);

                    Action onDismiss = null!;
                    onDismiss = () =>
                    {
                        toolbarVm.DismissRequested -= onDismiss;
                        completeCapture();
                    };
                    toolbarVm.DismissRequested += onDismiss;
                });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR capture failed: {ex.Message}");
            MessageBox.Show(
                $"Failed to capture screen region.\nError: {ex.Message}",
               "QuickTranslate - OCR Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
