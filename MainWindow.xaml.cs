using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using QuickTranslate.Models;
using QuickTranslate.Interop;

namespace QuickTranslate;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _autoHideTimer;

    public MainWindow()
    {
        InitializeComponent();
        
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Setup auto-hide timer (20 seconds)
        _autoHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(20)
        };
        _autoHideTimer.Tick += AutoHideTimer_Tick;

        // Subscribe to visibility changes to start/stop timer
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WindowVisibility))
            {
                if (_viewModel.WindowVisibility == Visibility.Visible)
                {
                    _autoHideTimer.Start();
                }
                else
                {
                    _autoHideTimer.Stop();
                }
            }
        };
    }

    /// <summary>
    /// Shows the window near the mouse cursor and starts translation
    /// </summary>
    public async void ShowAndTranslate(string selectedText)
    {
        // Position window near mouse cursor
        PositionNearCursor();

        // Start translation workflow with captured text
        await _viewModel.TranslateAsync(selectedText);
    }

    /// <summary>
    /// Positions the window near the current mouse cursor position with smart bounds checking
    /// </summary>
    private void PositionNearCursor()
    {
        if (NativeMethods.GetCursorPos(out NativeMethods.POINT cursorPos))
        {
            // Get DPI scaling factors
            var presentationSource = PresentationSource.FromVisual(this);
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;

            if (presentationSource?.CompositionTarget != null)
            {
                dpiScaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
            }

            // Convert cursor position from pixels to DIPs
            double cursorX = cursorPos.X / dpiScaleX;
            double cursorY = cursorPos.Y / dpiScaleY;

            // Get screen info (in pixels) and convert to DIPs
            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point(cursorPos.X, cursorPos.Y));
            
            double screenLeft = screen.WorkingArea.Left / dpiScaleX;
            double screenTop = screen.WorkingArea.Top / dpiScaleY;
            double screenRight = screen.WorkingArea.Right / dpiScaleX;
            double screenBottom = screen.WorkingArea.Bottom / dpiScaleY;

            // Determine window dimensions (use Actual if available, otherwise default/estimated)
            // If window is currently hidden, Actual sizes might be 0, so fallback to Width/Height or reasonable defaults
            double windowWidth = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 400 : Width);
            double windowHeight = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 300 : Height);

            // Default position: Bottom-Right of cursor
            // Add small offset to not cover the exact click point
            double left = cursorX + 10;
            double top = cursorY + 10;

            // Smart positioning logic (Menu behavior)
            
            // Check Right boundary
            if (left + windowWidth > screenRight)
            {
                // Flip to Left side of cursor
                left = cursorX - windowWidth - 10;
            }

            // Check Bottom boundary
            if (top + windowHeight > screenBottom)
            {
                // Flip to Top side of cursor
                top = cursorY - windowHeight - 10;
            }

            // Final safety clamp to ensuring it's always on screen
            // (e.g. if it's too big to fit on either side, prioritize Top/Left alignment)
            if (left < screenLeft) left = screenLeft;
            if (left + windowWidth > screenRight) left = screenRight - windowWidth;
            
            if (top < screenTop) top = screenTop;
            if (top + windowHeight > screenBottom) top = screenBottom - windowHeight;

            Left = left;
            Top = top;
        }
    }

    /// <summary>
    /// Hides window when clicked
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Auto-hide timer tick handler
    /// </summary>
    private void AutoHideTimer_Tick(object? sender, EventArgs e)
    {
        _viewModel.HideWindow();
        _autoHideTimer.Stop();
    }

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel.HideWindow();
    }

    private void Provider_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ProviderInfo provider)
        {
            _viewModel.SetProvider(provider.Name);
        }
    }
}
