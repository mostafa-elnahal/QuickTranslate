using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuickTranslate.Helpers;
using QuickTranslate.Models;
using QuickTranslate.Services.Input;
using QuickTranslate.ViewModels;
using Windows.Win32;
using WinForms = System.Windows.Forms;

namespace QuickTranslate.Views;

/// <summary>
/// Floating toolbar window that appears after text capture.
/// Positions itself near the cursor with DPI-aware screen-edge clamping.
/// </summary>
public partial class FloatingToolbarWindow : Window
{
    private readonly FloatingToolbarViewModel _viewModel;

    public FloatingToolbarWindow(FloatingToolbarViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        // Click-outside dismiss via event from ViewModel
        _viewModel.GlobalPointerDown += OnGlobalPointerDown;

        // Subscribe to dismiss event to reliably hide, bypassing overridden XAML DataBindings
        _viewModel.DismissRequested += () =>
        {
            if (Visibility != Visibility.Hidden)
            {
                Hide();
            }
        };
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// Positions the toolbar near the current cursor position.
    /// DPI-aware, with screen-edge clamping.
    /// </summary>
    private void PositionNearCursor()
    {
        if (!PInvoke.GetCursorPos(out System.Drawing.Point cursorPos))
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiScaleX = dpi.DpiScaleX;
        double dpiScaleY = dpi.DpiScaleY;

            // Convert cursor position from physical pixels to DIPs
            double cursorX = cursorPos.X / dpiScaleX;
            double cursorY = cursorPos.Y / dpiScaleY;

            // Get the working area of the screen containing the cursor
            var screen = WinForms.Screen.FromPoint(
                new System.Drawing.Point(cursorPos.X, cursorPos.Y));

            double screenLeft = screen.WorkingArea.Left / dpiScaleX;
            double screenTop = screen.WorkingArea.Top / dpiScaleY;
            double screenRight = screen.WorkingArea.Right / dpiScaleX;
            double screenBottom = screen.WorkingArea.Bottom / dpiScaleY;

            double windowWidth = ActualWidth > 0 ? ActualWidth : 40;
            double windowHeight = ActualHeight > 0 ? ActualHeight : 40;

            // Default: below and slightly left of cursor (so cursor doesn't overlap)
            double left = cursorX - (windowWidth / 2);
            double top = cursorY + 15;

            // Screen-edge clamping
            if (left + windowWidth > screenRight)
                left = screenRight - windowWidth;
            if (left < screenLeft)
                left = screenLeft;

            if (top + windowHeight > screenBottom)
                top = cursorY - windowHeight - 10; // Flip above cursor
            if (top < screenTop)
                top = screenTop;

            Left = left;
            Top = top;
    }

    public void ShowToolbar(string text, ToolbarDisplayMode mode)
    {
        DebugLog.Write($"ShowToolbar: text='{text}', mode={mode}, Visibility={Visibility}, IsVisible={_viewModel.IsVisible}");

        // Pre-position window synchronously before WPF updates visibility bindings
        // to prevent flickering in the old location.
        PositionNearCursor();

        _viewModel.Show(text, mode);
        
        DebugLog.Write($"ShowToolbar: after Show(), IsVisible={_viewModel.IsVisible}, Visibility={Visibility}, CapturedText='{_viewModel.CapturedText}'");

        // Ensure the WPF window is logically shown and participates in layout
        if (Visibility != Visibility.Visible)
        {
            Show();
            DebugLog.Write($"ShowToolbar: Show() called, Visibility now={Visibility}");
        }
    }

    /// <summary>
    /// Shows the toolbar for OCR capture results.
    /// Positions below the selection rectangle and sets the OCR bitmap.
    /// </summary>
    public void ShowToolbar(System.Drawing.Bitmap bitmap, System.Drawing.Rectangle selectionPhysicalPx)
    {
        DebugLog.Write($"ShowToolbar (OCR): bitmap={bitmap.Width}x{bitmap.Height}, rect=({selectionPhysicalPx.X},{selectionPhysicalPx.Y},{selectionPhysicalPx.Width},{selectionPhysicalPx.Height})");

        var dpi = VisualTreeHelper.GetDpi(this);

        var selDip = new Rect(
            selectionPhysicalPx.X / dpi.DpiScaleX,
            selectionPhysicalPx.Y / dpi.DpiScaleY,
            selectionPhysicalPx.Width / dpi.DpiScaleX,
            selectionPhysicalPx.Height / dpi.DpiScaleY);

        PositionBelowRectangle(selDip);
        _viewModel.SetOcrBitmap(bitmap);
        _viewModel.Show("", ToolbarDisplayMode.Ocr);

        if (Visibility != Visibility.Visible)
        {
            Show();
        }
    }

    /// <summary>
    /// Positions the toolbar centered below the given selection rectangle,
    /// with DPI-aware screen-edge clamping.
    /// </summary>
    private void PositionBelowRectangle(Rect selDip)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiScaleX = dpi.DpiScaleX;
        double dpiScaleY = dpi.DpiScaleY;

        var screen = WinForms.Screen.FromPoint(
            new System.Drawing.Point((int)((selDip.Left + selDip.Width / 2) * dpiScaleX),
                                      (int)((selDip.Top + selDip.Height / 2) * dpiScaleY)));

        double screenLeft = screen.WorkingArea.Left / dpiScaleX;
        double screenTop = screen.WorkingArea.Top / dpiScaleY;
        double screenRight = screen.WorkingArea.Right / dpiScaleX;
        double screenBottom = screen.WorkingArea.Bottom / dpiScaleY;

        double windowWidth = ActualWidth > 0 ? ActualWidth : 200;
        double windowHeight = ActualHeight > 0 ? ActualHeight : 40;

        // Default: centered horizontally below the selection rectangle
        double left = selDip.Left + (selDip.Width / 2) - (windowWidth / 2);
        double top = selDip.Bottom + 8;

        // Screen-edge clamping
        if (left + windowWidth > screenRight)
            left = screenRight - windowWidth;
        if (left < screenLeft)
            left = screenLeft;

        if (top + windowHeight > screenBottom)
            top = selDip.Top - windowHeight - 8; // Flip above rectangle
        if (top < screenTop)
            top = screenTop;

        Left = left;
        Top = top;
    }

    private void OnGlobalPointerDown(Point screenPoint)
    {
        if (!IsVisible)
        {
            DebugLog.Write($"OnGlobalPointerDown: ignored (IsVisible=false), screen=({screenPoint.X},{screenPoint.Y})");
            return;
        }

        Point localPoint = this.PointFromScreen(new Point(screenPoint.X, screenPoint.Y));

        bool insideWindow = localPoint.X >= 0 && localPoint.X <= ActualWidth &&
                            localPoint.Y >= 0 && localPoint.Y <= ActualHeight;

        DebugLog.Write($"OnGlobalPointerDown: screen=({screenPoint.X},{screenPoint.Y}), local=({localPoint.X},{localPoint.Y}), ActualSize=({ActualWidth},{ActualHeight}), insideWindow={insideWindow}");

        if (insideWindow)
        {
            _viewModel.OnPointerDownInsideToolbar();
        }
        else
        {
            DebugLog.Write($"OnGlobalPointerDown: DISMISSING (click outside)");
            _viewModel.DismissCommand.Execute(null);
        }
    }

    private async void Window_Deactivated(object sender, EventArgs e)
    {
        DebugLog.Write($"Window_Deactivated: IsActive={IsActive}, IsVisible={_viewModel.IsVisible}, waiting 100ms...");
        await Task.Delay(100);
        DebugLog.Write($"Window_Deactivated: after delay, IsActive={IsActive}, IsVisible={_viewModel.IsVisible}");
        if (!IsActive && IsVisible)
        {
            DebugLog.Write($"Window_Deactivated: calling DismissCommand");
            _viewModel.DismissCommand.Execute(null);
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _viewModel.OnMouseEnter();
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _viewModel.OnMouseLeave();
    }

    private void RootBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Clip = new RectangleGeometry(
                new Rect(0, 0, border.ActualWidth, border.ActualHeight), 8, 8);
        }
    }
}
