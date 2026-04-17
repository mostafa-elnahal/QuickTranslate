using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using System.Collections.Generic;
using QuickTranslate.Services.Providers;
using QuickTranslate.Services.Audio;
using System.Linq;

namespace QuickTranslate.Services;

public class PronunciationService : IPronunciationService
{
    private readonly IEnumerable<IPronunciationProvider> _providers;
    private readonly ISettingsService _settingsService;

    public PronunciationService(IEnumerable<IPronunciationProvider> providers, ISettingsService settingsService)
    {
        _providers = providers;
        _settingsService = settingsService;
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
        return await GetActiveProvider().GetPronunciationAsync(text);
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
