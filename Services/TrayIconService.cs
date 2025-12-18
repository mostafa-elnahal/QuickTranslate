using System;
using System.Drawing;
using System.Windows.Forms;
using QuickTranslate.Interop;

namespace QuickTranslate.Services;

public class TrayIconService : ITrayIconService
{
    private NotifyIcon? _trayIcon;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Visible = true,
            Text = "QuickTranslate - Press F1 to translate"
        };

        var contextMenu = new ContextMenuStrip();
        
        var showItem = new ToolStripMenuItem("Show Window");
        showItem.Click += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = contextMenu;

        _trayIcon.DoubleClick += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetVisible(bool visible)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = visible;
        }
    }

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

        IntPtr hIcon = bitmap.GetHicon();
        Icon tempIcon = Icon.FromHandle(hIcon);
        Icon clonedIcon = (Icon)tempIcon.Clone();
        NativeMethods.DestroyIcon(hIcon);
        bitmap.Dispose();
        return clonedIcon;
    }

    public void Dispose()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
    }
}
