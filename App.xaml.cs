using System;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslate.Services;
using QuickTranslate.ViewModels;

namespace QuickTranslate;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;

    // Services
    private IClipboardService? _clipboardService;
    private IHotkeyService? _hotkeyService;
    private ITrayIconService? _trayIconService;
    private IWindowPositioningService? _positioningService;
    private IWindowSizingService? _sizingService;
    private ISettingsService? _settingsService;

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
        _clipboardService = new ClipboardService();
        _hotkeyService = new HotkeyService();
        _trayIconService = new TrayIconService();
        _positioningService = new WindowPositioningService();
        _sizingService = new WindowSizingService(_settingsService);

        // Initialize ViewModel (Composition Root)
        var translationService = new GTranslateService();
        _viewModel = new MainViewModel(translationService, _settingsService!);

        // Create main window but don't show it
        _mainWindow = new MainWindow(_viewModel, _positioningService, _sizingService);

        // Setup system tray icon
        SetupTrayIcon();

        // Register global hotkey
        RegisterGlobalHotkey();

        // Listen for setting changes
        _settingsService!.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_settingsService != null && _hotkeyService != null && _mainWindow != null)
        {
            _hotkeyService.Register(HOTKEY_ID_TRANSLATE, _settingsService.Settings.Hotkey, _mainWindow);
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        // Cleanup
        _hotkeyService?.Dispose();
        _trayIconService?.Dispose();
        _viewModel?.Dispose();
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
        if (_settingsService == null) return;

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

        var viewModel = new SettingsViewModel(_settingsService);
        _settingsWindow = new SettingsWindow(viewModel);

        // Handle closure to clear reference
        _settingsWindow.Closed += (s, args) => _settingsWindow = null;

        _settingsWindow.Show();
    }

    private const int HOTKEY_ID_TRANSLATE = 1;

    private void RegisterGlobalHotkey()
    {
        if (_mainWindow == null || _hotkeyService == null) return;

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Initial registration
        if (_settingsService?.Settings.Hotkey != null)
        {
            _hotkeyService.Register(HOTKEY_ID_TRANSLATE, _settingsService.Settings.Hotkey, _mainWindow);
        }
    }

    /// <summary>
    /// Handles the hotkey press event
    /// </summary>
    private async void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (hotkeyId != HOTKEY_ID_TRANSLATE) return;

        // Instant feedback: Hide window immediately if open
        _viewModel?.HideWindow();

        if (_clipboardService == null) return;

        // Run capture on STA thread (required for clipboard/SendKeys)
        string capturedText = string.Empty;

        await Task.Run(() =>
        {
            var thread = new System.Threading.Thread(() =>
            {
                capturedText = _clipboardService.CaptureSelection();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        });

        // Show window and translate on UI thread
        _mainWindow?.ShowAndTranslate(capturedText);
    }
}
