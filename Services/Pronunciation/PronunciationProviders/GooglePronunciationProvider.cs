using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Helpers;
using QuickTranslate.Services.Audio;

namespace QuickTranslate.Services.Providers;

public class GooglePronunciationProvider : IPronunciationProvider
{
    private readonly ITranslationService _translationService;
    private readonly ISyllableService _syllableService;

    public string Name => Constants.PronunciationProviders.Google;

    /// <summary>
    /// Google provider does not support streaming (uses URL-based audio).
    /// </summary>
    public bool SupportsStreaming => false;

    public GooglePronunciationProvider(ITranslationService translationService, ISyllableService syllableService)
    {
        _translationService = translationService;
        _syllableService = syllableService;
    }

    /// <summary>
    /// Google does not support streaming. Returns failure.
    /// </summary>
    public Task<PronunciationResult<bool>> StreamAudioAsync(
        string text,
        string languageCode,
        bool slowMode,
        IStreamingAudioPlayer player,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PronunciationResult<bool>.Failure(
            "Streaming not supported by Google provider. Use Gemini for streaming."));
    }

    public async Task<PronunciationResult<PronunciationData>> GetPronunciationAsync(string text)
    {
        try
        {
            var data = new PronunciationData { OriginalText = text };

            if (string.IsNullOrWhiteSpace(text))
                return PronunciationResult<PronunciationData>.Success(data);

            // 1. Get translation to detect language and phonetics
            var result = await _translationService.TranslateAsync(text, "en");

            // Check for error pattern from GTranslateService
            if (!string.IsNullOrEmpty(result.MainTranslation) &&
                result.MainTranslation.StartsWith("[Translation Error:"))
            {
                var errorMsg = result.MainTranslation.Trim('[', ']');
                return PronunciationResult<PronunciationData>.Failure($"Translation Service Error: {errorMsg}");
            }

            data.DetectedLanguageCode = LanguageHelper.MapToIso6391(result.SourceLanguage);
            data.Phonetics = result.Phonetic;

            // 2. Generate Syllables
            try
            {
                var syllables = _syllableService.GetSyllables(text, result.Phonetic);
                foreach (var (syllableText, isStressed) in syllables)
                {
                    data.Syllables.Add(new SyllableItem
                    {
                        Text = syllableText,
                        IsStressed = isStressed
                    });
                }
            }
            catch (Exception ex)
            {
                // Non-critical, just log and continue without syllables
                System.Diagnostics.Debug.WriteLine($"Syllable generation failed: {ex}");
            }

            // 3. Generate default Audio URI (normal speed)
            // Note: Internal call to GetAudioUriAsync is usually safe, but check result
            var audioResult = await GetAudioUriAsync(text, data.DetectedLanguageCode, false);
            if (audioResult.IsSuccess)
            {
                data.AudioUri = audioResult.Data;
            }

            return PronunciationResult<PronunciationData>.Success(data);
        }
        catch (Exception ex)
        {
            return PronunciationResult<PronunciationData>.Failure("Failed to load pronunciation data.", ex);
        }
    }

    public Task<PronunciationResult<Uri?>> GetAudioUriAsync(string text, string languageCode, bool slowMode)
    {
        if (string.IsNullOrEmpty(text))
            return Task.FromResult(PronunciationResult<Uri?>.Success(null));

        try
        {
            var encodedText = Uri.EscapeDataString(text);
            string speedParam = slowMode ? "&ttsspeed=0.15" : "";
            var uri = new Uri($"https://translate.google.com/translate_tts?ie=UTF-8&q={encodedText}&tl={languageCode}&client=tw-ob{speedParam}");

            return Task.FromResult(PronunciationResult<Uri?>.Success(uri));
        }
        catch (Exception ex)
        {
            return Task.FromResult(PronunciationResult<Uri?>.Failure("Failed to generate audio link.", ex));
        }
    }
}
