using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Application = System.Windows.Application;
using QuickTranslate.Interop;
using QuickTranslate.Helpers;

namespace QuickTranslate;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const int HOTKEY_ID = 9000;
    private NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private HwndSource? _hwndSource;

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
        System.Windows.MessageBox.Show($"Application Crashed. Log saved to crash_log.txt.\n{e.Exception.Message}", "QuickTranslate Error");
        e.Handled = true; // Prevent app from closing immediately
        Shutdown();
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Create main window but don't show it
        _mainWindow = new MainWindow();

        // Setup system tray icon
        SetupTrayIcon();

        // Register global hotkey (Ctrl + Shift + T)
        RegisterGlobalHotkey();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        // Cleanup
        UnregisterGlobalHotkey();
        
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
    }

    /// <summary>
    /// Sets up the system tray icon
    /// </summary>
    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Visible = true,
            Text = "QuickTranslate - Press F1 to translate"
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();
        
        var showItem = new ToolStripMenuItem("Show Window");
        showItem.Click += (s, e) =>
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
        };
        
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => Shutdown();

        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = contextMenu;

        // Double-click to show window
        _trayIcon.DoubleClick += (s, e) =>
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
        };
    }

    /// <summary>
    /// Creates a simple 'T' icon for the system tray
    /// </summary>
    private Icon CreateTrayIcon()
    {
        // Create a 16x16 bitmap
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            
            // Draw a simple 'T' letter
            using (var font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            {
                g.DrawString("T", font, brush, -2, -2);
            }
        }

        // Properly handle GDI icon to avoid memory leak
        IntPtr hIcon = bitmap.GetHicon();
        Icon tempIcon = Icon.FromHandle(hIcon);
        Icon clonedIcon = (Icon)tempIcon.Clone();
        NativeMethods.DestroyIcon(hIcon); // Release the GDI handle
        bitmap.Dispose();
        return clonedIcon;
    }

    /// <summary>
    /// Registers the global hotkey (F1)
    /// </summary>
    private void RegisterGlobalHotkey()
    {
        if (_mainWindow == null) return;

        // Get window handle
        var helper = new WindowInteropHelper(_mainWindow);
        var handle = helper.EnsureHandle();

        // Create HwndSource to hook into Win32 message loop
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);

        // Register hotkey: F1 (0x70) with no modifiers
        bool success = NativeMethods.RegisterHotKey(
            handle,
            HOTKEY_ID,
            0, // No modifiers
            0x70); // F1 key

        if (!success)
        {
            System.Windows.MessageBox.Show(
                "Failed to register hotkey F1. It may be in use by another application.",
                "QuickTranslate",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Unregisters the global hotkey
    /// </summary>
    private void UnregisterGlobalHotkey()
    {
        if (_mainWindow == null) return;

        var helper = new WindowInteropHelper(_mainWindow);
        var handle = helper.Handle;

        if (handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(handle, HOTKEY_ID);
        }
    }

    /// <summary>
    /// Window procedure to handle Win32 messages
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            // Hotkey pressed - trigger the translation workflow
            OnHotkeyPressed();
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Handles the hotkey press event
    /// </summary>
    private async void OnHotkeyPressed()
    {
        // Run capture on STA thread (required for clipboard/SendKeys)
        string capturedText = string.Empty;
        
        await Task.Run(() =>
        {
            var thread = new System.Threading.Thread(() =>
            {
                capturedText = CaptureSelection();
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        });
        
        // Show window and translate on UI thread
        _mainWindow?.ShowAndTranslate(capturedText);
    }
    
    /// <summary>
    /// EXACT copy of ClipboardCaptureStrategy.Capture() from console app
    /// </summary>
    private string CaptureSelection()
    {
        Log("Starting Clipboard Strategy...");
        
        // 1. Preserve ALL clipboard formats
        ClipboardHelper.ClipboardSnapshot? snapshot = null;
        try 
        {
            snapshot = ClipboardHelper.SaveSnapshot();
            if (snapshot.HasContent)
            {
                Log($"Saved clipboard snapshot - Text: {snapshot.Text != null}, Files: {snapshot.Files?.Count ?? 0}, Image: {snapshot.Image != null}, HTML: {snapshot.Html != null}, RTF: {snapshot.Rtf != null}");
            }
            else
            {
                Log("Clipboard was empty.");
            }
        }
        catch (Exception ex) 
        {
            Log($"Failed to save clipboard: {ex.Message}");
        }

        System.Threading.Thread.Sleep(50);
        
        // 2. Clear & Copy selection
        ClipboardHelper.ClearSafe();
        System.Windows.Forms.SendKeys.SendWait("^c");

        // 3. Wait for clipboard to populate
        // GetTextWithTimeout handles retries and exceptions internally
        string capturedText = ClipboardHelper.GetTextWithTimeout();
        if (!string.IsNullOrEmpty(capturedText))
        {
            Log($"Captured: {capturedText}");
        }

        // 4. Restore original clipboard WITHOUT adding to history
        try
        {
            if (snapshot != null && snapshot.HasContent)
            {
                Log("Restoring clipboard without history...");
                ClipboardHelper.RestoreWithoutHistory(snapshot);
            }
            else
            {
                Log("Clearing clipboard without history...");
                ClipboardHelper.ClearWithoutHistory();
            }
        }
        finally
        {
            snapshot?.Dispose();
        }

        if (string.IsNullOrEmpty(capturedText))
        {
            Log("Clipboard capture failed or was empty.");
        }

        return capturedText;
    }

    private void Log(string message)
    {
        try
        {
            System.IO.File.AppendAllText("debug.log", $"{DateTime.Now}: {message}{Environment.NewLine}");
        }
        catch { }
    }
}
