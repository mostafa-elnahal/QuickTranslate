using System;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslate.Services;
using QuickTranslate.ViewModels;
using QuickTranslate.Views;
using QuickTranslate.Services.Providers;
using System.Collections.Generic;

using Microsoft.Extensions.DependencyInjection;

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
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Start services
        var trayIconService = _serviceProvider.GetRequiredService<ITrayIconService>();
        trayIconService.Initialize();
        trayIconService.ExitRequested += (s, args) => Shutdown();
        trayIconService.SettingsRequested += (s, args) => OpenSettingsWindow();

        // Register hotkeys
        RegisterGlobalHotkeys();

        // Listen for setting changes
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        settingsService.SettingsChanged += OnSettingsChanged;
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
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISyllableService, SyllableService>();

        // Conditional Sizing Service (factory pattern for legacy support if needed, but here simple)
        services.AddSingleton<IWindowSizingService>(sp => 
            new WindowSizingService(sp.GetRequiredService<ISettingsService>()));

        // Pronunciation Providers & Service
        services.AddSingleton<IPronunciationProvider>(sp => 
            new GooglePronunciationProvider(
                sp.GetRequiredService<ITranslationService>(), 
                sp.GetRequiredService<ISyllableService>()));
        
        services.AddSingleton<IPronunciationProvider>(sp => 
            new GeminiPronunciationProvider(
                sp.GetRequiredService<ITranslationService>(), 
                sp.GetRequiredService<ISyllableService>(),
                sp.GetRequiredService<ISettingsService>()));

        services.AddSingleton<IPronunciationService>(sp => 
            new PronunciationService(
                sp.GetServices<IPronunciationProvider>(), 
                sp.GetRequiredService<ISettingsService>()));

        // ViewModels
        services.AddSingleton<PopupViewModel>();
        services.AddSingleton<PronunciationViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Windows/Views
        services.AddSingleton<TranslationPopup>();
        services.AddSingleton<PronunciationPopup>();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_serviceProvider != null)
        {
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            RegisterTranslationHotkey(settingsService.Settings.Hotkey);
            RegisterPronunciationHotkey(settingsService.Settings?.PronunciationHotkey ?? "Ctrl+Shift+P");
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

    #endregion

    /// <summary>
    /// Handles the hotkey press event
    /// </summary>
    private async void OnHotkeyPressed(object? sender, int hotkeyId)
    {
        if (_serviceProvider == null) return;
        var clipboardService = _serviceProvider.GetRequiredService<IClipboardService>();

        // Run capture on STA thread (required for clipboard/SendKeys)
        string capturedText = string.Empty;

        try
        {
            var tcs = new TaskCompletionSource<string>();
            var thread = new System.Threading.Thread(() =>
            {
                try { tcs.SetResult(clipboardService.CaptureSelection()); }
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
}
