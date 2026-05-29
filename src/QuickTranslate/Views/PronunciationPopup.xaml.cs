using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using QuickTranslate.Services;
using QuickTranslate.ViewModels;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.Foundation;

namespace QuickTranslate.Views;

/// <summary>
/// Pronunciation popup window - displays source word with pronunciation audio and karaoke animation.
/// </summary>
public partial class PronunciationPopup : Window
{
    private readonly PronunciationViewModel _viewModel;
    private readonly IWindowPositioningService _positioningService;
    private readonly IWindowSizingService _sizingService;
    private readonly System.Windows.Threading.DispatcherTimer _progressTimer;
    private bool _isDraggingSlider;
    private bool _isUpdatingFromTimer;

    // Default constructor for XAML designer support
    public PronunciationPopup()
    {
        InitializeComponent();
        _viewModel = null!;
        _positioningService = null!;
        _sizingService = null!;
        _progressTimer = new System.Windows.Threading.DispatcherTimer();
    }

    public PronunciationPopup(PronunciationViewModel viewModel, IWindowPositioningService positioningService, IWindowSizingService sizingService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _positioningService = positioningService;
        _sizingService = sizingService;
        DataContext = _viewModel;

        // Initialize progress timer
        _progressTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _progressTimer.Tick += ProgressTimer_Tick;


        // Sync timer with IsPlaying property for streaming support
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsPlaying))
            {
                if (_viewModel.IsPlaying) _progressTimer.Start();
                else _progressTimer.Stop();
            }
        };
    }

    /// <summary>
    /// Shows the window near the mouse cursor and loads pronunciation data.
    /// </summary>
    public async void ShowAndPronounce(string text)
    {
        // 1. Prepare the window with initial text (shows loading state)
        _viewModel.PrepareForLoading(text);

        // 2. Capture generation for race condition check
        int myGeneration = _viewModel.PronunciationGeneration;

        // 3. Position and show window IMMEDIATELY (before data loads)
        Opacity = 0;
        _viewModel.IsVisible = true;

        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        UpdateLayout();

        _positioningService.PositionNearCursor(this);
        Opacity = 1.0;

        // 4. Now load pronunciation data asynchronously (window already visible with spinner)
        await _viewModel.LoadPronunciationAsync(text);

        // 5. Guard against race conditions (user may have triggered new request)
        if (_viewModel.PronunciationGeneration != myGeneration)
        {
            return;
        }

    }

    #region Event Handlers

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DisableMaximization();
        _sizingService?.ApplySize(this, WindowType.Pronunciation);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        // Ensure the window is activated when clicked
        if (!IsActive || !IsKeyboardFocusWithin)
        {
            Activate();
            Focus();
        }
    }

    private void DisableMaximization()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var style = PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        PInvoke.SetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE, style & ~(int)WINDOW_STYLE.WS_MAXIMIZEBOX);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsVisible && WindowState == WindowState.Normal)
        {
            _sizingService?.SaveSize(this, WindowType.Pronunciation);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.HideWindow();
    }

    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        if (_viewModel == null || _isDraggingSlider) return;

        if (_viewModel.StreamingPlayer != null)
        {
            _isUpdatingFromTimer = true;
            _viewModel.TotalDuration = _viewModel.StreamingPlayer.TotalDuration;
            _viewModel.CurrentPosition = _viewModel.StreamingPlayer.CurrentPosition;
            _isUpdatingFromTimer = false;
        }
    }

    private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _isDraggingSlider = false;
        
        if (_viewModel != null)
        {
            var slider = sender as System.Windows.Controls.Slider;
            if (slider != null)
            {
                TimeSpan newPos = TimeSpan.FromSeconds(slider.Value);
                if (newPos > _viewModel.TotalDuration) newPos = _viewModel.TotalDuration;

                _viewModel.StreamingPlayer?.SetPosition(newPos);
            }
        }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingFromTimer && _viewModel != null && !_isDraggingSlider)
        {
            // User initiated click (not drag, since DragCompleted handles drag)
            TimeSpan newPos = TimeSpan.FromSeconds(e.NewValue);

            // Limit to duration
            if (newPos > _viewModel.TotalDuration) newPos = _viewModel.TotalDuration;

            if (_viewModel.StreamingPlayer != null)
            {
                _viewModel.StreamingPlayer.SetPosition(newPos);
            }

            // Update VM immediately for smooth UI
            _isUpdatingFromTimer = true;
            _viewModel.CurrentPosition = newPos;
            _isUpdatingFromTimer = false;
        }
        else if (_isDraggingSlider && _viewModel != null)
        {
            // Track position for UI label only while dragging
            _isUpdatingFromTimer = true;
            _viewModel.CurrentPosition = TimeSpan.FromSeconds(e.NewValue);
            _isUpdatingFromTimer = false;
        }
    }



    #endregion
}
