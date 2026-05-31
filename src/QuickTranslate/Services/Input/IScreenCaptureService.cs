using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Service for capturing screen regions. Abstracted to allow testing and different implementations.
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Shows a fullscreen overlay for the user to select a screen region.
    /// Returns the captured bitmap, or null if the user cancels.
    /// </summary>
    Task<Bitmap?> CaptureRegionAsync(CancellationToken cancellationToken = default);
}
