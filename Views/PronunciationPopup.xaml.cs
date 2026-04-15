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

        // Subscribe to ViewModel events for playback control
        _viewModel.RequestPlayFromView += (s, e) =>
        {
            AudioPlayer.Play();
            _progressTimer.Start();
        };
        _viewModel.RequestPauseFromView += (s, e) =>
        {
            AudioPlayer.Pause();
            _progressTimer.Stop();
        };
        _viewModel.RequestRestartFromView += (s, e) =>
        {
            AudioPlayer.Stop();
            AudioPlayer.Play();
            _progressTimer.Start();
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

        // 6. Auto-play audio after data is ready
        PlayAudio();
    }

    private void PlayAudio()
    {
        System.Diagnostics.Debug.WriteLine($"PlayAudio called. AudioUri: {_viewModel?.AudioUri}");

        if (_viewModel?.AudioUri == null)
        {
            System.Diagnostics.Debug.WriteLine("AudioUri is null, cannot play");
            return;
        }

        try
        {
            // Show loading spinner while buffering/loading media
            _viewModel.IsLoading = true;
            _viewModel.StatusMessage = string.Empty; // Clear previous errors

            // Reset position to allow replay
            AudioPlayer.Position = TimeSpan.Zero;

            // Only set Source if it changed (or if null) to avoid unnecessary reloading
            // But if we want to ensure it works, setting it is safer usually, unless it causes lag.
            // However, the issue is likely position not creating a re-trigger.
            // Let's set Source AND Position.
            if (AudioPlayer.Source != _viewModel.AudioUri)
            {
                AudioPlayer.Source = _viewModel.AudioUri;
            }

            // Set speed (always normal playback, as source file handles slowness)
            AudioPlayer.SpeedRatio = 1.0;

            System.Diagnostics.Debug.WriteLine($"Playing audio: {_viewModel.AudioUri}, SpeedRatio: {AudioPlayer.SpeedRatio}");
            AudioPlayer.Play();
            _viewModel.IsPlaying = true;
            _progressTimer.Start();

            // Trigger animation if media is already loaded (MediaOpened won't fire)
            if (AudioPlayer.NaturalDuration.HasTimeSpan)
            {
                // If it's already ready, clear loading immediately
                _viewModel.IsLoading = false;
                StartAnimation();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio playback error: {ex.Message}");
            _viewModel.IsPlaying = false;
            _viewModel.IsLoading = false;
            _viewModel.StatusMessage = "Playback Error";
        }
    }

    // Removed StartKaraokeAnimation / ResetKaraokeAnimation

    private void ResetAnimation()
    {
        // Cancel any running animation in VM if needed (VM handles logic via IsPlaying flag)
        foreach (var s in _viewModel.Syllables) s.IsActive = false;
    }

    private async void StartAnimation()
    {
        if (_viewModel == null) return;

        // Get audio duration
        TimeSpan duration = AudioPlayer.NaturalDuration.HasTimeSpan
            ? AudioPlayer.NaturalDuration.TimeSpan
            : TimeSpan.FromSeconds(1.5);

        // Adjust for speed ratio (now always 1.0 since we use server-side slowing)
        double ratio = 1.0;

        // Effective duration is longer if slower
        TimeSpan effectiveDuration = duration / ratio;

        await _viewModel.AnimateSyllablesAsync(effectiveDuration);
    }

    #region Event Handlers

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DisableMaximization();
        _sizingService?.ApplySize(this, WindowType.Pronunciation);
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
        AudioPlayer.Stop();
        _viewModel.HideWindow();
    }

    private void ProgressTimer_Tick(object? sender, EventArgs e)
    {
        if (_viewModel == null || _isDraggingSlider) return;

        if (AudioPlayer.Source != null && AudioPlayer.NaturalDuration.HasTimeSpan)
        {
            _isUpdatingFromTimer = true;
            _viewModel.TotalDuration = AudioPlayer.NaturalDuration.TimeSpan;
            _viewModel.CurrentPosition = AudioPlayer.Position;
            _isUpdatingFromTimer = false;

            // Auto-stop if we reach the end (MediaEnded sometimes fails to fire if very short)
            if (AudioPlayer.Position >= AudioPlayer.NaturalDuration.TimeSpan)
            {
                if (_viewModel.IsPlaying)
                {
                    AudioPlayer.Stop();
                    _viewModel.IsPlaying = false;

                    // Reset to start
                    _isUpdatingFromTimer = true;
                    _viewModel.CurrentPosition = TimeSpan.Zero;
                    _isUpdatingFromTimer = false;
                }
            }
        }
    }

    private void Slider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _isDraggingSlider = false;
        // Logic handled by ValueChanged now
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingFromTimer && _viewModel != null)
        {
            // User initiated change (Click or Drag)
            TimeSpan newPos = TimeSpan.FromSeconds(e.NewValue);

            // Limit to duration
            if (newPos > _viewModel.TotalDuration) newPos = _viewModel.TotalDuration;

            // Update audio
            AudioPlayer.Position = newPos;

            // Update VM immediately for smooth UI
            _isUpdatingFromTimer = true;
            _viewModel.CurrentPosition = newPos;
            _isUpdatingFromTimer = false;
        }
    }

    private void AudioPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        _viewModel.IsLoading = false;
        if (AudioPlayer.NaturalDuration.HasTimeSpan)
        {
            _viewModel.TotalDuration = AudioPlayer.NaturalDuration.TimeSpan;
        }
        StartAnimation();
    }

    private void AudioPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPlaying = false;
        _progressTimer.Stop();

        // Reset to start so it's ready to play again
        AudioPlayer.Stop();

        _isUpdatingFromTimer = true;
        _viewModel.CurrentPosition = TimeSpan.Zero;
        _isUpdatingFromTimer = false;
    }

    private void AudioPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Media failed: {e.ErrorException?.Message}");
        _viewModel.IsPlaying = false;
        _viewModel.IsLoading = false;
        _progressTimer.Stop();

        // Show friendly error in status bar
        if (_viewModel != null)
        {
            _viewModel.StatusMessage = InterpretError(e.ErrorException);
        }
    }

    private string InterpretError(Exception? ex)
    {
        if (ex == null) return "Audio playback failed.";

        var msg = ex.Message;

        // Network / DNS errors
        if (msg.Contains("remote name could not be resolved") || msg.Contains("connect to the remote server"))
            return "No Internet Connection.";

        // HTTP Status Codes
        if (msg.Contains("(404)")) return "Pronunciation not available.";
        if (msg.Contains("(429)")) return "Google Limit Reached (Try later).";
        if (msg.Contains("(503)")) return "Service Unavailable.";
        if (msg.Contains("(403)")) return "Access Denied.";

        // Media Element cryptic errors
        if (msg.Contains("HRESULT") || msg.Contains("0xC00D"))
            return "Audio Unavailable (Format/Network).";

        // Fallback: shorten very long messages
        if (msg.Length > 50) return msg.Substring(0, 47) + "...";

        return msg;
    }

    #endregion
}
