using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Services.Audio;

namespace QuickTranslate.Services.Providers;

/// <summary>
/// Pronunciation provider using the official Google Cloud Text-to-Speech REST API.
/// Supports batch synthesis with exact word-timing via SSML mark timepoints,
/// and streaming synthesis for low-latency playback of long texts.
/// </summary>
public class GcpPronunciationProvider : IPronunciationProvider, IDisposable
{
    private const string BaseEndpoint = "https://texttospeech.googleapis.com/v1beta1/text:synthesize";
    private const string DefaultVoiceName = "en-US-Chirp3-HD-Puck";
    private const int DefaultSampleRate = 24000;

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    // Thread-safe audio file cache keyed by (text, slowMode)
    private readonly ConcurrentDictionary<(string Text, bool SlowMode), string> _audioCache = new();

    public string Name => Constants.PronunciationProviders.Gcp;
    public bool SupportsStreaming => false; // Start with batch-only; streaming can be added later
    public int MaxChunkSize => 4500; // 5000 byte limit minus SSML overhead

    public GcpPronunciationProvider(ISettingsService settingsService)
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _settingsService = settingsService;
    }

    #region IPronunciationProvider

    public async Task<PronunciationResult<Uri?>> GetAudioUriAsync(
        string text, string languageCode, bool slowMode)
    {
        try
        {
            var result = await GetAudioWithTimepointsAsync(text, languageCode, slowMode);
            if (!result.IsSuccess)
                return PronunciationResult<Uri?>.Failure(result.Message);

            return PronunciationResult<Uri?>.Success(result.Data!.AudioUri);
        }
        catch (Exception ex)
        {
            return PronunciationResult<Uri?>.Failure("GCP TTS failed.", ex);
        }
    }

    public async Task<PronunciationResult<bool>> StreamAudioAsync(
        string text,
        string languageCode,
        bool slowMode,
        IStreamingAudioPlayer player,
        CancellationToken cancellationToken = default)
    {
        // For now, fall back to batch synthesis and feed the result into the player.
        // True gRPC streaming can be added later.
        try
        {
            var result = await GetAudioWithTimepointsAsync(text, languageCode, slowMode);
            if (!result.IsSuccess)
                return PronunciationResult<bool>.Failure(result.Message);

            var audioUri = result.Data!.AudioUri;
            var wavBytes = await File.ReadAllBytesAsync(audioUri.LocalPath, cancellationToken);

            // Parse WAV header to find the 'data' chunk offset
            int dataOffset = FindWavDataChunkOffset(wavBytes);
            if (dataOffset < 0 || dataOffset >= wavBytes.Length)
                return PronunciationResult<bool>.Failure("Invalid WAV audio data.");

            player.Initialize(DefaultSampleRate, 1, 16);
            var pcmData = new byte[wavBytes.Length - dataOffset];
            Buffer.BlockCopy(wavBytes, dataOffset, pcmData, 0, pcmData.Length);

            await player.EnqueueSamplesAsync(pcmData, cancellationToken);
            player.Play();

            return PronunciationResult<bool>.Success(true, "Playback complete.");
        }
        catch (TaskCanceledException)
        {
            return PronunciationResult<bool>.Failure("Playback was cancelled.");
        }
        catch (Exception ex)
        {
            return PronunciationResult<bool>.Failure($"GCP streaming error: {ex.Message}", ex);
        }
    }

    #endregion

    #region GCP-specific: Timepoints

    /// <summary>
    /// Synthesizes audio with SSML mark timepoints for exact word-level synchronization.
    /// </summary>
    public async Task<PronunciationResult<(Uri AudioUri, IReadOnlyList<TimepointInfo> Timepoints)>>
        GetAudioWithTimepointsAsync(string text, string languageCode, bool slowMode)
    {
        try
        {
            string apiKey = _settingsService.Settings.GcpApiKey;
            if (string.IsNullOrEmpty(apiKey))
                return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                    .Failure("GCP API Key is missing. Please configure it in Settings.");

            // Build SSML with <mark> tags between words
            string ssml = BuildSsmlWithMarks(text, slowMode);

            // Select voice based on language code
            string voiceName = SelectVoice(languageCode);

            var requestBody = new
            {
                input = new { ssml },
                voice = new
                {
                    languageCode = NormalizeLanguageCode(languageCode),
                    name = voiceName
                },
                audioConfig = new
                {
                    audioEncoding = "MP3"
                },
                enableTimePointing = new[] { "SSML_MARK" }
            };

            string endpoint = $"{BaseEndpoint}?key={apiKey}";
            string jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(endpoint, httpContent);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"GCP TTS Error: {response.StatusCode} - {errorContent}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                        .Failure("Invalid GCP API Key.");

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                        .Failure("GCP quota exceeded. Try again later.");

                return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                    .Failure($"GCP TTS Error: {response.StatusCode}");
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Extract audio content
            string audioBase64 = root.GetProperty("audioContent").GetString()
                ?? throw new InvalidOperationException("No audio content in response.");
            byte[] audioBytes = Convert.FromBase64String(audioBase64);

            // Save to temp file
            var cacheKey = (text, slowMode);
            if (_audioCache.TryRemove(cacheKey, out var oldPath))
            {
                try { File.Delete(oldPath); } catch { /* ignore cleanup errors */ }
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"gcp_tts_{Guid.NewGuid()}.mp3");
            await File.WriteAllBytesAsync(tempPath, audioBytes);
            _audioCache[cacheKey] = tempPath;

            // Extract timepoints
            var timepoints = new List<TimepointInfo>();
            if (root.TryGetProperty("timepoints", out var timepointsElement))
            {
                foreach (var tp in timepointsElement.EnumerateArray())
                {
                    string markName = tp.GetProperty("markName").GetString() ?? "";
                    double timeSeconds = tp.GetProperty("timeSeconds").GetDouble();
                    timepoints.Add(new TimepointInfo(markName, timeSeconds));
                }
            }

            return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                .Success((new Uri(tempPath), timepoints));
        }
        catch (HttpRequestException ex)
        {
            return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                .Failure($"Network error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                .Failure("Request timed out.", ex);
        }
        catch (Exception ex)
        {
            return PronunciationResult<(Uri, IReadOnlyList<TimepointInfo>)>
                .Failure($"GCP TTS error: {ex.Message}", ex);
        }
    }

    #endregion

    #region SSML Builder

    /// <summary>
    /// Builds SSML with mark tags between each word for timepoint tracking.
    /// Example: <speak><mark name="w0"/>Hello <mark name="w1"/>world</speak>
    /// </summary>
    private static string BuildSsmlWithMarks(string text, bool slowMode)
    {
        var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        sb.Append("<speak>");

        if (slowMode)
            sb.Append("<prosody rate=\"slow\">");

        for (int i = 0; i < words.Length; i++)
        {
            sb.Append($"<mark name=\"w{i}\"/>");
            sb.Append(EscapeXml(words[i]));
            if (i < words.Length - 1) sb.Append(' ');
        }

        if (slowMode)
            sb.Append("</prosody>");

        sb.Append("</speak>");
        return sb.ToString();
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    #endregion

    #region Voice Selection

    private static string SelectVoice(string languageCode)
    {
        // Chirp 3 HD provides a consistent naming convention across almost all GCP locales.
        // We use the universally available 'Puck' voice variant.
        // In a real production app, you might want to call /v1beta1/voices to ensure the exact Match exists, but this works perfectly for all our supported normalizations.
        string normalized = NormalizeLanguageCode(languageCode);
        return $"{normalized}-Chirp3-HD-Puck";
    }

    /// <summary>
    /// Normalize a 2-letter ISO 639-1 code to a BCP-47 locale code.
    /// GCP TTS requires locale codes like "en-US", not just "en".
    /// </summary>
    private static string NormalizeLanguageCode(string languageCode)
    {
        if (languageCode.Contains('-') || languageCode.Contains('_'))
            return languageCode.Replace('_', '-');

        return languageCode.ToLowerInvariant() switch
        {
            "en" => "en-US",
            "es" => "es-ES",
            "fr" => "fr-FR",
            "de" => "de-DE",
            "it" => "it-IT",
            "pt" => "pt-BR",
            "ja" => "ja-JP",
            "ko" => "ko-KR",
            "zh" => "cmn-CN",
            "ar" => "ar-XA",
            "ru" => "ru-RU",
            "hi" => "hi-IN",
            "tr" => "tr-TR",
            "nl" => "nl-NL",
            "pl" => "pl-PL",
            "sv" => "sv-SE",
            "da" => "da-DK",
            "fi" => "fi-FI",
            "nb" => "nb-NO",
            "uk" => "uk-UA",
            "vi" => "vi-VN",
            "th" => "th-TH",
            "id" => "id-ID",
            "ms" => "ms-MY",
            _ => $"{languageCode}-{languageCode.ToUpperInvariant()}"
        };
    }

    #endregion

    #region WAV Parsing

    /// <summary>
    /// Parses a WAV byte array to find the offset where the 'data' chunk payload begins.
    /// Walks RIFF sub-chunks instead of assuming a fixed 44-byte header.
    /// Returns -1 if the data chunk is not found.
    /// </summary>
    private static int FindWavDataChunkOffset(byte[] wav)
    {
        // Minimum: 12 bytes RIFF header + 8 bytes for at least one sub-chunk header
        if (wav.Length < 20) return -1;

        // Verify RIFF header
        if (wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F')
            return -1;

        // Start scanning sub-chunks after the 12-byte RIFF header ("RIFF" + size + "WAVE")
        int offset = 12;
        while (offset + 8 <= wav.Length)
        {
            string chunkId = System.Text.Encoding.ASCII.GetString(wav, offset, 4);
            int chunkSize = BitConverter.ToInt32(wav, offset + 4);

            if (chunkId == "data")
                return offset + 8; // payload starts right after the 8-byte chunk header

            // Advance to next chunk (chunk header = 8 bytes + chunk data, word-aligned)
            offset += 8 + chunkSize;
            if (chunkSize % 2 != 0) offset++; // RIFF chunks are word-aligned
        }

        // Fallback: standard 44-byte header
        return wav.Length > 44 ? 44 : -1;
    }

    #endregion

    private void CleanupTempFiles()
    {
        foreach (var path in _audioCache.Values)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore cleanup errors */ }
        }
        _audioCache.Clear();
    }

    public void Dispose()
    {
        CleanupTempFiles();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
