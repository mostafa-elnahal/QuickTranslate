using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Helpers;
using QuickTranslate.Services.Audio;
using System.Net.Http;
using System.IO;

namespace QuickTranslate.Services.Providers;

public class GooglePronunciationProvider : IPronunciationProvider
{
    private readonly ITranslationService _translationService;
    private readonly ISyllableService _syllableService;

    private static readonly HttpClient _httpClient = new HttpClient();

    static GooglePronunciationProvider()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://translate.google.com/");
    }

    public string Name => Constants.PronunciationProviders.Google;

    /// <summary>
    /// Google provider now supports streaming via chunked TTS requests.
    /// </summary>
    public bool SupportsStreaming => true;

    /// <summary>
    /// Unofficial Google TTS limit is 200 characters.
    /// </summary>
    public int MaxChunkSize => 150;

    public GooglePronunciationProvider(ITranslationService translationService, ISyllableService syllableService)
    {
        _translationService = translationService;
        _syllableService = syllableService;
    }

    /// <summary>
    /// Streams audio by downloading MP3 chunks and decoding to PCM.
    /// </summary>
    public async Task<PronunciationResult<bool>> StreamAudioAsync(
        string text,
        string languageCode,
        bool slowMode,
        IStreamingAudioPlayer player,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Add a small delay between chunk requests to avoid 429 (Rate Limit) 
            // errors from Google's unofficial endpoint.
            await Task.Delay(300, cancellationToken);

            var audioResult = await GetAudioUriAsync(text, languageCode, slowMode);
            if (!audioResult.IsSuccess || audioResult.Data == null)
                return PronunciationResult<bool>.Failure(audioResult.Message);

            using var response = await _httpClient.GetAsync(audioResult.Data, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorDetail = await response.Content.ReadAsStringAsync(cancellationToken);
                return PronunciationResult<bool>.Failure(
                    $"Google TTS Error: {response.StatusCode} ({(int)response.StatusCode}). {errorDetail}");
            }

            var mp3Bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            
            using (var ms = new System.IO.MemoryStream(mp3Bytes))
            using (var reader = new NAudio.Wave.Mp3FileReader(ms))
            {
                var format = reader.WaveFormat;
                player.Initialize(format.SampleRate, format.Channels, format.BitsPerSample);

                byte[] buffer = new byte[32768]; // 32KB buffer
                int read;
                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    byte[] pcmChunk = new byte[read];
                    Buffer.BlockCopy(buffer, 0, pcmChunk, 0, read);
                    player.EnqueueSamples(pcmChunk);
                }
            }

            player.Play();
            return PronunciationResult<bool>.Success(true);
        }
        catch (HttpRequestException ex)
        {
            return PronunciationResult<bool>.Failure($"Network error connecting to Google: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            return PronunciationResult<bool>.Failure("Google streaming failed.", ex);
        }
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
            // If text is long, we force streaming mode by NOT returning an AudioUri here.
            if (text.Length <= MaxChunkSize)
            {
                var audioResult = await GetAudioUriAsync(text, data.DetectedLanguageCode, false);
                if (audioResult.IsSuccess)
                {
                    data.AudioUri = audioResult.Data;
                }
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
