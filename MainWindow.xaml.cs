using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using QuickTranslate.Services;
using QuickTranslate.ViewModels;
using QuickTranslate.Models;

namespace QuickTranslate;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IWindowPositioningService _positioningService;

    // Default constructor for XAML designer support (optional/fake)
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = null!;
        _positioningService = null!;
    }

    public MainWindow(MainViewModel viewModel, IWindowPositioningService positioningService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _positioningService = positioningService;
        DataContext = _viewModel;
    }

    /// <summary>
    /// Shows the window near the mouse cursor and starts translation
    /// </summary>
    public async void ShowAndTranslate(string selectedText)
    {
        // 1. Start translation (Window is Collapsed from ViewModel start)
        await _viewModel.TranslateAsync(selectedText);

        // 2. Prepare for sizing: Make visible but transparent
        // This forces WPF to calculate the size based on the new content
        Opacity = 0;
        _viewModel.WindowVisibility = Visibility.Visible;

        // 3. Force Layout Update
        // We use Dispatcher priority Render to ensure layout is calculated before we measure/move
        await Dispatcher.InvokeAsync(() =>
        {
            // Safety Check: Reset WindowState if it somehow got Maximized (fixes crash with ShowActivated=False)
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }

            // Force layout update to get correct ActualWidth/Height
            UpdateLayout();

            // 4. Position window near mouse cursor using REAL size
            _positioningService.PositionNearCursor(this);

            // 5. Show instantly
            Opacity = 1.0;
        }, DispatcherPriority.Render);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DisableMaximization();
    }

    private void DisableMaximization()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var style = QuickTranslate.Interop.NativeMethods.GetWindowLong(hwnd, QuickTranslate.Interop.NativeMethods.GWL_STYLE);
        QuickTranslate.Interop.NativeMethods.SetWindowLong(hwnd, QuickTranslate.Interop.NativeMethods.GWL_STYLE, style & ~QuickTranslate.Interop.NativeMethods.WS_MAXIMIZEBOX);
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


    private void CloseButton_Click(object sender, RoutedEventArgs e)
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
