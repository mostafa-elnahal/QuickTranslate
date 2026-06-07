using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Services.Audio;

namespace QuickTranslate.Services.Providers;

public class ElevenLabsPronunciationProvider : IPronunciationProvider, IDisposable
{
    private const string BaseEndpoint = "https://api.elevenlabs.io/v1/text-to-speech";
    private const string DefaultVoiceId = "21m00Tcm4TlvDq8ikWAM";
    private const string ModelId = "eleven_multilingual_v2";
    private const int DefaultSampleRate = 24000;
    private const int HttpTimeoutSeconds = 30;
    private const int MaxCacheEntries = 50;

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly ConcurrentDictionary<string, CachedAudio> _audioCache = new();
    private readonly LinkedList<string> _cacheOrder = new();
    private readonly object _cacheLock = new();

    public string Name => Constants.PronunciationProviders.ElevenLabs;
    public bool SupportsStreaming => true;
    public int MaxChunkSize => 5000;
    public TimingSupportLevel TimingSupport => TimingSupportLevel.Exact;

    public ElevenLabsPronunciationProvider(ISettingsService settingsService)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds) };
        _settingsService = settingsService;
    }

    public Task<PronunciationResult<Uri?>> GetAudioUriAsync(string text, string languageCode, bool slowMode)
    {
        // Audio is loaded lazily via StreamAudioAsync and cached in-memory.
        // URI retrieval is no longer supported after removing temp file I/O.
        return Task.FromResult(PronunciationResult<Uri?>.Success(null));
    }

    public async Task<PronunciationResult<bool>> StreamAudioAsync(
        string text,
        string languageCode,
        bool slowMode,
        IStreamingAudioPlayer player,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string apiKey = _settingsService.Settings.ElevenLabsApiKey;
            if (string.IsNullOrEmpty(apiKey))
                return PronunciationResult<bool>.Failure("ElevenLabs API Key is missing.");

            string voiceId = _settingsService.Settings.ElevenLabsVoiceId;
            if (string.IsNullOrEmpty(voiceId))
                voiceId = DefaultVoiceId;

            string cacheKey = BuildCacheKey(voiceId, text, slowMode);

            // Check in-memory cache first to avoid redundant API calls
            if (!TryGetCached(cacheKey, out var cachedAudio))
            {
                // Cache miss: download via GetAudioWithTimepointsAsync (also populates cache)
                var result = await GetAudioWithTimepointsAsync(text, languageCode, slowMode);
                if (!result.IsSuccess)
                    return PronunciationResult<bool>.Failure(result.Message);

                TryGetCached(cacheKey, out cachedAudio);
            }

            if (cachedAudio.PcmData.Length == 0)
                return PronunciationResult<bool>.Failure("Invalid ElevenLabs audio data.");

            player.Initialize(DefaultSampleRate, 1, 16);

            // Deliver cached timepoints to the player so the animation system
            // can use exact word timing instead of heuristics
            if (cachedAudio.Timepoints?.Count > 0)
                player.SetChunkTimepoints(cachedAudio.Timepoints, cachedAudio.PcmData.Length);

            await player.EnqueueSamplesAsync(cachedAudio.PcmData, cancellationToken);
            player.Play();

            return PronunciationResult<bool>.Success(true);
        }
        catch (TaskCanceledException)
        {
            return PronunciationResult<bool>.Failure("Playback was cancelled.");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            return PronunciationResult<bool>.Failure("Invalid ElevenLabs API Key.");
        }
        catch (Exception ex)
        {
            return PronunciationResult<bool>.Failure($"ElevenLabs streaming error: {ex.Message}", ex);
        }
    }

    public async Task<PronunciationResult<(Uri AudioUri, IReadOnlyList<TimepointInfo> Timepoints)>>
        GetAudioWithTimepointsAsync(string text, string languageCode, bool slowMode)
    {
        try
        {
            string apiKey = _settingsService.Settings.ElevenLabsApiKey;
            if (string.IsNullOrEmpty(apiKey))
                return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                    .Failure("ElevenLabs API Key is missing.");

            string voiceId = _settingsService.Settings.ElevenLabsVoiceId;
            if (string.IsNullOrEmpty(voiceId))
                voiceId = DefaultVoiceId;

            var cacheKey = BuildCacheKey(voiceId, text, slowMode);
            if (TryGetCached(cacheKey, out var cachedAudio))
            {
                return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                    .Success((CacheUri, cachedAudio.Timepoints));
            }

            string endpoint = $"{BaseEndpoint}/{voiceId}/with-timestamps?output_format=pcm_24000";

            var requestBody = new
            {
                text,
                model_id = ModelId,
                voice_settings = new
                {
                    stability = 0.35,
                    similarity_boost = 0.85
                }
            };

            string jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = httpContent,
                Headers = { { "xi-api-key", apiKey } }
            };

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"ElevenLabs API Error: {response.StatusCode} - {errorBody}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                        .Failure("Invalid ElevenLabs API Key.");

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                        .Failure("ElevenLabs rate limit exceeded. Try again later.");

                return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                    .Failure(ExtractErrorMessage(errorBody, response.StatusCode));
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string audioBase64 = root.GetProperty("audio_base64").GetString()
                ?? throw new InvalidOperationException("No audio content in response.");
            byte[] audioBytes = Convert.FromBase64String(audioBase64);

            var timepoints = ExtractTimepoints(root, text);

            SetCached(cacheKey, new CachedAudio(audioBytes, timepoints));

            return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                .Success((CacheUri, timepoints));
        }
        catch (HttpRequestException ex)
        {
            return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                .Failure($"Network error: {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                .Failure("Request timed out.");
        }
        catch (JsonException ex)
        {
            return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                .Failure($"Invalid response from ElevenLabs: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                .Failure($"ElevenLabs TTS error: {ex.Message}", ex);
        }
    }

    private IReadOnlyList<TimepointInfo> ExtractTimepoints(JsonElement root, string originalText)
    {
        // Prefer normalized_alignment (matches what TTS actually spoke), fall back to alignment
        JsonElement alignment = default;
        bool useNormalized = root.TryGetProperty("normalized_alignment", out var normalized) &&
                             normalized.ValueKind == JsonValueKind.Object;
        if (useNormalized)
            alignment = normalized;
        else if (root.TryGetProperty("alignment", out var rawAlignment))
            alignment = rawAlignment;

        if (alignment.ValueKind != JsonValueKind.Object)
            return Array.Empty<TimepointInfo>();

        var charsElement = alignment.GetProperty("characters");
        var startTimesElement = alignment.GetProperty("character_start_times_seconds");

        int charCount = charsElement.GetArrayLength();
        var alignmentChars = new string[charCount];
        var startTimes = new double[charCount];

        for (int i = 0; i < charCount; i++)
        {
            alignmentChars[i] = charsElement[i].GetString() ?? " ";
            startTimes[i] = startTimesElement[i].GetDouble();
        }

        return ConvertToWordTimepoints(originalText, alignmentChars, startTimes);
    }

    private static List<TimepointInfo> ConvertToWordTimepoints(
        string originalText, string[] alignmentChars, double[] startTimes)
    {
        var words = originalText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var timepoints = new List<TimepointInfo>(words.Length);

        int charIdx = 0;
        for (int w = 0; w < words.Length; w++)
        {
            while (charIdx < alignmentChars.Length && alignmentChars[charIdx] == " ")
                charIdx++;

            double wordStart = charIdx < startTimes.Length ? startTimes[charIdx] :
                startTimes.Length > 0 ? startTimes[^1] : 0.0;

            timepoints.Add(new TimepointInfo($"w{w}", wordStart));

            charIdx += words[w].Length;
            while (charIdx < alignmentChars.Length && alignmentChars[charIdx] == " ")
                charIdx++;
        }

        return timepoints;
    }

    private static string ExtractErrorMessage(string errorBody, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            var detail = doc.RootElement.GetProperty("detail");
            if (detail.TryGetProperty("message", out var message))
                return message.GetString() ?? $"ElevenLabs API Error: {statusCode}";
        }
        catch
        {
            // Fall through to the generic status message.
        }

        return $"ElevenLabs API Error: {statusCode}";
    }

    private static string BuildCacheKey(string voiceId, string text, bool slowMode)
        => $"{voiceId}|{slowMode}|{text}";

    private bool TryGetCached(string key, out CachedAudio? cached)
    {
        if (_audioCache.TryGetValue(key, out cached))
        {
            lock (_cacheLock)
            {
                _cacheOrder.Remove(key);
                _cacheOrder.AddLast(key);
            }
            return true;
        }
        return false;
    }

    private void SetCached(string key, CachedAudio cached)
    {
        lock (_cacheLock)
        {
            _audioCache[key] = cached;
            _cacheOrder.AddLast(key);
            while (_cacheOrder.Count > MaxCacheEntries)
            {
                var oldest = _cacheOrder.First!.Value;
                _cacheOrder.RemoveFirst();
                _audioCache.TryRemove(oldest, out _);
            }
        }
    }

    private static readonly Uri CacheUri = new("memory://cached");

    /// <summary>
    /// Clears the in-memory audio cache. Called when the pronunciation popup closes
    /// to release retained PCM data.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _audioCache.Clear();
            _cacheOrder.Clear();
        }
    }

    public void Dispose()
    {
        lock (_cacheLock)
        {
            _audioCache.Clear();
            _cacheOrder.Clear();
        }
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record CachedAudio(byte[] PcmData, IReadOnlyList<TimepointInfo> Timepoints);
}
