using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
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

        // Reposition whenever the toolbar becomes visible
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FloatingToolbarViewModel.IsVisible) && _viewModel.IsVisible)
            {
                PositionNearCursor();
            }
        };

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

    /// <summary>
    /// Positions the toolbar near the current cursor position.
    /// DPI-aware, with screen-edge clamping.
    /// </summary>
    private void PositionNearCursor()
    {
        // Defer until layout completes so ActualWidth/ActualHeight are accurate
        Dispatcher.BeginInvoke(new Action(() =>
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
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void ShowToolbar(string text, ToolbarDisplayMode mode)
    {
        _viewModel.Show(text, mode);
        
        // Ensure the WPF window is logically shown and participates in layout
        if (Visibility != Visibility.Visible)
        {
            Show();
        }
    }

    private void OnGlobalPointerDown(Point screenPoint)
    {
        if (!IsVisible) return;

        Point localPoint = this.PointFromScreen(new Point(screenPoint.X, screenPoint.Y));

        bool insideWindow = localPoint.X >= 0 && localPoint.X <= ActualWidth &&
                            localPoint.Y >= 0 && localPoint.Y <= ActualHeight;

        if (!insideWindow)
        {
            _viewModel.DismissCommand.Execute(null);
        }
    }

    private async void Window_Deactivated(object sender, EventArgs e)
    {
        await Task.Delay(100);
        if (!IsActive && IsVisible)
        {
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
}
