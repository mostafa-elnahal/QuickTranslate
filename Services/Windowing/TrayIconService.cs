using System;
using System.Drawing;
using System.Windows.Forms;
using Windows.Win32;


namespace QuickTranslate.Services;

public class TrayIconService : ITrayIconService
{
    private NotifyIcon? _trayIcon;
    private Icon? _icon;

    public event EventHandler? ExitRequested;
    public event EventHandler? SettingsRequested;

    public void Initialize()
    {
        try
        {
            var resourceUri = new Uri("pack://application:,,,/Assets/ico-v2.ico");
            var streamInfo = System.Windows.Application.GetResourceStream(resourceUri);
            if (streamInfo != null)
            {
                _icon = new Icon(streamInfo.Stream);
            }
        }
        catch (Exception ex)
        {
            // Fallback or log? For now we just won't have an icon if this fails, 
            // or we could throw. But since user explicitly asked for this, 
            // let's trust it works. If it fails, _icon will be null.
            // NotifyIcon needs an Icon or it might throw or show nothing.
            // Let's create a default or empty icon if null? 
            // Actually, SystemIcons.Application is a good fallback.
            _icon = (Icon)SystemIcons.Application.Clone();
            System.Diagnostics.Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
        }

        _trayIcon = new NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            Text = "QuickTranslate"
        };

        var contextMenu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = contextMenu;
    }

    public void SetVisible(bool visible)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = visible;
        }
    }

    public void Dispose()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _icon?.Dispose();
    }
}
