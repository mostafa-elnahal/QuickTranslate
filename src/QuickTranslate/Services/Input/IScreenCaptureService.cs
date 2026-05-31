using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Service for capturing screen regions. Abstracted to allow testing and different implementations.
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Shows a fullscreen overlay for the user to select a screen region.
    /// The overlay stays visible after selection until the caller invokes the completeCapture callback.
    /// Returns the captured bitmap with selection bounds, or null if the user cancels.
    /// </summary>
    Task<ScreenCaptureResult?> CaptureRegionAsync(
        CancellationToken cancellationToken = default,
        Action<ScreenCaptureResult, Action>? onRegionCaptured = null);
}
