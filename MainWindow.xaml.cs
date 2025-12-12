using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

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
    public async void ShowAndTranslate()
    {
        // Position window near mouse cursor
        PositionNearCursor();

        // Start translation workflow
        await _viewModel.TranslateClipboardAsync();
    }

    /// <summary>
    /// Positions the window near the current mouse cursor position
    /// </summary>
    private void PositionNearCursor()
    {
        if (NativeMethods.GetCursorPos(out NativeMethods.POINT cursorPos))
        {
            // Offset the window slightly from cursor (20px right, 20px down)
            Left = cursorPos.X + 20;
            Top = cursorPos.Y + 20;

            // Ensure window stays within screen bounds
            var screen = System.Windows.Forms.Screen.FromPoint(
                new System.Drawing.Point(cursorPos.X, cursorPos.Y));

            if (Left + Width > screen.WorkingArea.Right)
                Left = screen.WorkingArea.Right - Width;

            if (Top + Height > screen.WorkingArea.Bottom)
                Top = screen.WorkingArea.Bottom - Height;

            if (Left < screen.WorkingArea.Left)
                Left = screen.WorkingArea.Left;

            if (Top < screen.WorkingArea.Top)
                Top = screen.WorkingArea.Top;
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
