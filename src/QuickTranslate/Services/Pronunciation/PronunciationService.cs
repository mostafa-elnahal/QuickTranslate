using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using System.Collections.Generic;
using QuickTranslate.Services.Providers;
using QuickTranslate.Services.Audio;
using System.Linq;
using QuickTranslate.Helpers;

namespace QuickTranslate.Services;

public class PronunciationService : IPronunciationService
{
    private readonly IEnumerable<IPronunciationProvider> _providers;
    private readonly ISettingsService _settingsService;
    private readonly ITranslationService _translationService;
    private readonly ISyllableService _syllableService;

    public PronunciationService(
        IEnumerable<IPronunciationProvider> providers, 
        ISettingsService settingsService,
        ITranslationService translationService,
        ISyllableService syllableService)
    {
        _providers = providers;
        _settingsService = settingsService;
        _translationService = translationService;
        _syllableService = syllableService;
    }

    /// <summary>
    /// Whether the active provider supports streaming audio.
    /// </summary>
    public bool SupportsStreaming => GetActiveProvider().SupportsStreaming;

    /// <summary>
    /// The maximum number of characters allowed per chunk for the active provider.
    /// </summary>
    public int MaxChunkSize => GetActiveProvider().MaxChunkSize;

    private IPronunciationProvider GetActiveProvider()
    {
        string providerName = _settingsService.Settings.PronunciationProvider;
        // Case-insensitive match, fallback to "Google"
        return _providers.FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase))
               ?? _providers.FirstOrDefault(p => p.Name.Equals(Constants.PronunciationProviders.Google, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException("No pronunciation providers available.");
    }

    public async Task<PronunciationResult<PronunciationData>> GetPronunciationAsync(string text)
    {
        var data = new PronunciationData { OriginalText = text };

        if (string.IsNullOrWhiteSpace(text))
            return PronunciationResult<PronunciationData>.Success(data);

        var provider = GetActiveProvider();

        try
        {
            // 1. Get translation to detect language and phonetics
            var result = await _translationService.TranslateAsync(text, "en");

            // Check for error pattern from GTranslateService
            if (!result.IsSuccess)
            {
                return PronunciationResult<PronunciationData>.Failure($"Translation Service Error: {result.ErrorMessage}");
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

            // 3. Generate Audio URI if text is small enough for the provider chunk size
            if (text.Length <= provider.MaxChunkSize)
            {
                // GCP provider: fetch audio + timepoints in a single API call
                if (provider is GcpPronunciationProvider gcpProvider)
                {
                    var gcpResult = await gcpProvider.GetAudioWithTimepointsAsync(
                        text, data.DetectedLanguageCode, false);
                    if (gcpResult.IsSuccess)
                    {
                        data.AudioUri = gcpResult.Data!.AudioUri;
                        data.Timepoints = gcpResult.Data.Timepoints;
                    }
                }
                else
                {
                    var audioResult = await provider.GetAudioUriAsync(text, data.DetectedLanguageCode, false);
                    if (audioResult.IsSuccess)
                    {
                        data.AudioUri = audioResult.Data;
                    }
                }
            }

            return PronunciationResult<PronunciationData>.Success(data);
        }
        catch (Exception ex)
        {
            return PronunciationResult<PronunciationData>.Failure("Failed to load pronunciation data.", ex);
        }
    }

    public async Task<PronunciationResult<Uri?>> GetAudioUriAsync(string text, string languageCode, bool slowMode)
    {
        return await GetActiveProvider().GetAudioUriAsync(text, languageCode, slowMode);
    }

    public async Task<PronunciationResult<bool>> StreamAudioAsync(
        string text,
        string languageCode,
        bool slowMode,
        IStreamingAudioPlayer player,
        CancellationToken cancellationToken = default)
    {
        return await GetActiveProvider().StreamAudioAsync(text, languageCode, slowMode, player, cancellationToken);
    }
}
