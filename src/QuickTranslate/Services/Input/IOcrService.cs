using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;

namespace QuickTranslate.Services.Input;

/// <summary>
/// Service for performing OCR (Optical Character Recognition) on screen regions or images.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Captures a screen region selected by the user and recognizes text from it.
    /// Returns null if the user cancels the selection.
    /// </summary>
    Task<string?> CaptureAndRecognizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognizes text from a bitmap image using the specified or current language.
    /// </summary>
    Task<string> RecognizeFromBitmapAsync(Bitmap bitmap, string? languageCode = null, bool singleLine = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of available OCR languages installed on the system.
    /// </summary>
    IReadOnlyList<OcrLanguage> GetAvailableLanguages();

    /// <summary>
    /// Gets or sets the current OCR language code (e.g., "en-US", "fr-FR").
    /// </summary>
    string CurrentLanguageCode { get; set; }
}
