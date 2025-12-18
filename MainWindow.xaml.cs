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
    private readonly DispatcherTimer _autoHideTimer;

    // Default constructor for XAML designer support (optional/fake)
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = null!;
        _positioningService = null!;
        _autoHideTimer = null!;
    }

    public MainWindow(MainViewModel viewModel, IWindowPositioningService positioningService)
    {
        InitializeComponent();
        
        _viewModel = viewModel;
        _positioningService = positioningService;
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
        // Position window near mouse cursor using service
        _positioningService.PositionNearCursor(this);

        // Start translation workflow with captured text
        await _viewModel.TranslateAsync(selectedText);
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
