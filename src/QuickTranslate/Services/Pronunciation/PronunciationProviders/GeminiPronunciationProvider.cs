using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Helpers;
using QuickTranslate.Services.Audio;

namespace QuickTranslate.Services.Providers;

public class GeminiPronunciationProvider : IPronunciationProvider, IDisposable
{
    private const string ModelName = "gemini-2.5-flash-preview-tts";
    private const string DefaultVoice = "Kore";

    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    // Thread-safe audio file cache keyed by (text, slowMode)
    private readonly ConcurrentDictionary<(string Text, bool SlowMode), string> _audioCache = new();
    private string _lastLanguageCode = "en";

    public string Name => Constants.PronunciationProviders.Gemini;

    /// <summary>
    /// Gemini supports streaming via HTTP streaming API.
    /// </summary>
    public bool SupportsStreaming => true;

    /// <summary>
    /// Gemini handles large chunks effectively.
    /// </summary>
    public int MaxChunkSize => 4000;

    public GeminiPronunciationProvider(ISettingsService settingsService)
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10) // Increase timeout for long text generation
        };
        _settingsService = settingsService;
    }



    public async Task<PronunciationResult<Uri?>> GetAudioUriAsync(string text, string languageCode, bool slowMode)
    {
        try
        {
            var cacheKey = (text, slowMode);

            // Thread-safe cache check
            if (_audioCache.TryGetValue(cacheKey, out var cachedPath) && File.Exists(cachedPath))
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiProvider] Cache HIT for '{text}' (slow={slowMode})");
                return PronunciationResult<Uri?>.Success(new Uri(cachedPath));
            }

            System.Diagnostics.Debug.WriteLine($"[GeminiProvider] Cache MISS: generating audio for '{text}'");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var uri = await GenerateAudioAsync(text, slowMode, cts.Token);

            _lastLanguageCode = languageCode;

            return PronunciationResult<Uri?>.Success(uri);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("API Key"))
        {
            return PronunciationResult<Uri?>.Failure("Invalid Gemini API Key.", ex);
        }
        catch (HttpRequestException ex)
        {
            return PronunciationResult<Uri?>.Failure("Network error connecting to Gemini.", ex);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key is missing"))
        {
            return PronunciationResult<Uri?>.Failure("Gemini API Key is missing.", ex);
        }
        catch (TaskCanceledException ex)
        {
            return PronunciationResult<Uri?>.Failure("Request timed out.", ex);
        }
        catch (Exception ex)
        {
            return PronunciationResult<Uri?>.Failure("An unexpected error occurred.", ex);
        }
    }

    private async Task<Uri> GenerateAudioAsync(string text, bool slowMode, CancellationToken cancellationToken = default)
    {
        string apiKey = _settingsService.Settings.GeminiApiKey;

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Gemini API key is missing. Please configure it in Settings.");
        }

        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent?key={apiKey}";

        // Build prompt - use slow speech instruction if slow mode
        string prompt = slowMode
            ? $"Please speak this text slowly and clearly for pronunciation practice: {text}"
            : $"Please pronounce this text clearly: {text}";

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                responseModalities = new[] { "AUDIO" },
                speechConfig = new
                {
                    voiceConfig = new
                    {
                        prebuiltVoiceConfig = new { voiceName = DefaultVoice }
                    }
                }
            }
        };

        string jsonContent = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _httpClient.PostAsync(endpoint, httpContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Gemini API Error: {response.StatusCode} - {errorContent}");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new HttpRequestException("Gemini API Key is invalid.");
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new HttpRequestException("Gemini quota exceeded. Try again later.");
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                throw new HttpRequestException("Invalid request to Gemini API.");

            throw new HttpRequestException($"Gemini API Error: {response.StatusCode}");
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(responseBody);


        var root = doc.RootElement;
        var candidates = root.GetProperty("candidates");
        var firstCandidate = candidates[0];
        var parts = firstCandidate.GetProperty("content").GetProperty("parts");
        var audioPart = parts[0];

        if (!audioPart.TryGetProperty("inlineData", out var inlineData))
        {
            throw new InvalidOperationException("No audio data found in Gemini response.");
        }

        string base64Audio = inlineData.GetProperty("data").GetString()
            ?? throw new InvalidOperationException("Audio data is null.");
        byte[] pcmBytes = Convert.FromBase64String(base64Audio);

        // Convert PCM to WAV
        byte[] wavBytes = ConvertPcmToWav(pcmBytes, 24000, 1, 16);

        // Save to temp file and cache the path
        var tempPath = Path.Combine(Path.GetTempPath(), $"gemini_audio_{Guid.NewGuid()}.wav");
        await File.WriteAllBytesAsync(tempPath, wavBytes);

        // Evict any stale entry for this key before adding
        var cacheKey = (text, slowMode);
        if (_audioCache.TryRemove(cacheKey, out var oldPath))
        {
            try { File.Delete(oldPath); } catch { /* ignore cleanup errors */ }
        }
        _audioCache[cacheKey] = tempPath;

        return new Uri(tempPath);
    }

    private static byte[] ConvertPcmToWav(byte[] pcmData, int sampleRate, int channels, int bitsPerSample)
    {
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;

        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);

        // RIFF header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmData.Length); // File size - 8
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // Subchunk1Size (16 for PCM)
        writer.Write((short)1); // AudioFormat (1 = PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);

        // data subchunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Streams audio from Gemini API using streamGenerateContent endpoint.
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
            string apiKey = _settingsService.Settings.GeminiApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                return PronunciationResult<bool>.Failure("Gemini API Key is missing.");
            }

            // Use streamGenerateContent for streaming response
            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:streamGenerateContent?alt=sse&key={apiKey}";

            string prompt = slowMode
                ? $"Please speak this text slowly and clearly for pronunciation practice: {text}"
                : $"Please pronounce this text clearly: {text}";

            // TODO: consider refactoring the anonymous type to a class
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new
                    {
                        voiceConfig = new
                        {
                            prebuiltVoiceConfig = new { voiceName = DefaultVoice }
                        }
                    }
                }
            };

            string jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Initialize player for 24kHz mono 16-bit PCM (Gemini's output format)
            player.Initialize(24000, 1, 16);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = httpContent };
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"Gemini Streaming Error: {response.StatusCode} - {errorContent}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return PronunciationResult<bool>.Failure("Invalid Gemini API Key.");

                return PronunciationResult<bool>.Failure($"Gemini API Error: {response.StatusCode}");
            }

            // Read SSE stream
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            bool playbackStarted = false;

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line)) continue;

                // SSE format: "data: {...json...}"
                if (line.StartsWith("data: "))
                {
                    string jsonData = line.Substring(6);
                    if (jsonData == "[DONE]") break;

                    try
                    {
                        using var doc = JsonDocument.Parse(jsonData);
                        var root = doc.RootElement;

                        // Navigate to audio data: candidates[0].content.parts[0].inlineData.data
                        if (root.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0)
                        {
                            var candidate = candidates[0];
                            if (candidate.TryGetProperty("content", out var content) &&
                                content.TryGetProperty("parts", out var parts) &&
                                parts.GetArrayLength() > 0)
                            {
                                var part = parts[0];
                                if (part.TryGetProperty("inlineData", out var inlineData) &&
                                    inlineData.TryGetProperty("data", out var dataElement))
                                {
                                    string base64Audio = dataElement.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(base64Audio))
                                    {
                                        byte[] pcmChunk = Convert.FromBase64String(base64Audio);
                                        await player.EnqueueSamplesAsync(pcmChunk, cancellationToken);

                                        if (!playbackStarted)
                                        {
                                            player.Play();
                                            playbackStarted = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"JSON parse error in stream: {ex.Message}");
                    }
                }
            }

            return PronunciationResult<bool>.Success(true, "Streaming complete.");
        }
        catch (HttpRequestException ex)
        {
            return PronunciationResult<bool>.Failure($"Network error: {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            return PronunciationResult<bool>.Failure("Streaming was cancelled.");
        }
        catch (Exception ex)
        {
            return PronunciationResult<bool>.Failure($"Streaming error: {ex.Message}", ex);
        }
    }

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
