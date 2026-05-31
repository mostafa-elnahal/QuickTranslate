using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QuickTranslate.Views;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Screen capture service that uses a singleton fullscreen overlay for region selection.
/// Only one overlay instance exists at a time — repeated triggers cancel the previous
/// selection and start fresh.
/// </summary>
public class ScreenCaptureService : IScreenCaptureService, IDisposable
{
    private CancellationTokenSource? _currentCts;

    public async Task<Bitmap?> CaptureRegionAsync(CancellationToken cancellationToken = default)
    {
        // Ensure we're on the UI thread
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            return await Application.Current.Dispatcher.InvokeAsync(
                () => CaptureRegionAsync(cancellationToken)).Result;
        }

        // Cancel any existing selection overlay so only one is active at a time
        _currentCts?.Cancel();
        _currentCts?.Dispose();
        _currentCts = new CancellationTokenSource();

        var overlay = new ScreenSelectionOverlay();
        return await overlay.CaptureAsync(_currentCts.Token);
    }

    public void Dispose()
    {
        _currentCts?.Cancel();
        _currentCts?.Dispose();
        _currentCts = null;
    }
}
