using System;
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
    private readonly ITranslationService _translationService;
    private readonly ISyllableService _syllableService;
    private readonly ISettingsService _settingsService;

    private string? _lastTempFilePath;
    private string _lastText = string.Empty;
    private string _lastLanguageCode = "en";
    private bool _lastSlowMode = false;

    public string Name => Constants.PronunciationProviders.Gemini;

    /// <summary>
    /// Gemini supports streaming via HTTP streaming API.
    /// </summary>
    public bool SupportsStreaming => true;

    /// <summary>
    /// Gemini handles large chunks effectively.
    /// </summary>
    public int MaxChunkSize => 4000;

    public GeminiPronunciationProvider(
        ITranslationService translationService,
        ISyllableService syllableService,
        ISettingsService settingsService)
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10) // Increase timeout for long text generation
        };
        _translationService = translationService;
        _syllableService = syllableService;
        _settingsService = settingsService;
    }

    public async Task<PronunciationResult<PronunciationData>> GetPronunciationAsync(string text)
    {
        try
        {
            var data = new PronunciationData { OriginalText = text };

            if (string.IsNullOrWhiteSpace(text))
                return PronunciationResult<PronunciationData>.Success(data);

            // 1. Get translation (same as Google)
            var result = await _translationService.TranslateAsync(text, "en");
            data.DetectedLanguageCode = LanguageHelper.MapToIso6391(result.SourceLanguage);
            data.Phonetics = result.Phonetic;
            _lastLanguageCode = data.DetectedLanguageCode;

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
                System.Diagnostics.Debug.WriteLine($"Syllable generation failed: {ex}");
            }

            // 3. Generate audio
            var audioResult = await GetAudioUriAsync(text, data.DetectedLanguageCode, false);
            if (audioResult.IsSuccess)
            {
                data.AudioUri = audioResult.Data;
            }

            return PronunciationResult<PronunciationData>.Success(data);
        }
        catch (Exception ex)
        {
            return PronunciationResult<PronunciationData>.Failure("Failed to load pronunciation.", ex);
        }
    }

    public async Task<PronunciationResult<Uri?>> GetAudioUriAsync(string text, string languageCode, bool slowMode)
    {
        try
        {
            // Cache check
            if (_lastTempFilePath != null &&
                text == _lastText &&
                slowMode == _lastSlowMode &&
                File.Exists(_lastTempFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"[GeminiProvider] Cache HIT for '{text}'");
                return PronunciationResult<Uri?>.Success(new Uri(_lastTempFilePath));
            }

            System.Diagnostics.Debug.WriteLine($"[GeminiProvider] Cache MISS: generating audio for '{text}'");

            CleanupTempFile();

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
            var uri = await GenerateAudioAsync(text, slowMode, cts.Token);

            _lastText = text;
            _lastLanguageCode = languageCode;
            _lastSlowMode = slowMode;

            return PronunciationResult<Uri?>.Success(uri);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("API Key"))
        {
            return PronunciationResult<Uri?>.Failure("Invalid Gemini API Key.", ex);
        }
        catch (HttpRequestException ex)
        {
            // Generic network/API error
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

    private async Task<Uri> GenerateAudioAsync(string text, bool slowMode, System.Threading.CancellationToken cancellationToken = default)
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

        response.EnsureSuccessStatusCode();

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

        // Save to temp file
        CleanupTempFile();
        _lastTempFilePath = Path.Combine(Path.GetTempPath(), $"gemini_audio_{Guid.NewGuid()}.wav");
        await File.WriteAllBytesAsync(_lastTempFilePath, wavBytes);

        return new Uri(_lastTempFilePath);
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
                                        player.EnqueueSamples(pcmChunk);

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

    private void CleanupTempFile()
    {
        if (_lastTempFilePath != null && File.Exists(_lastTempFilePath))
        {
            try { File.Delete(_lastTempFilePath); } catch { /* Ignore cleanup errors */ }
        }
    }

    public void Dispose()
    {
        CleanupTempFile();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
