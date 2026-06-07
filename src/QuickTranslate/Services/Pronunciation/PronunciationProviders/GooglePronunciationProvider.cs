using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Helpers;
using QuickTranslate.Services.Audio;
using System.Net.Http;
using System.IO;
using NAudio.Wave;

namespace QuickTranslate.Services.Providers;

public class GooglePronunciationProvider : IPronunciationProvider, IDisposable
{
    private readonly HttpClient _httpClient;

    public GooglePronunciationProvider()
    {
        _httpClient = new HttpClient();
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

    public TimingSupportLevel TimingSupport => TimingSupportLevel.Estimated;

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
            var audioResult = await GetAudioUriAsync(text, languageCode, slowMode);
            if (!audioResult.IsSuccess || audioResult.Data == null)
                return PronunciationResult<bool>.Failure(audioResult.Message);

            using var request = new HttpRequestMessage(HttpMethod.Get, audioResult.Data);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Referer", "https://translate.google.com/");
            
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorDetail = await response.Content.ReadAsStringAsync(cancellationToken);
                return PronunciationResult<bool>.Failure(
                    $"Google TTS Error: {response.StatusCode} ({(int)response.StatusCode}). {errorDetail}");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType == null || !mediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                string preview = await response.Content.ReadAsStringAsync(cancellationToken);
                preview = preview.Length > 200 ? preview[..200] + "..." : preview;
                return PronunciationResult<bool>.Failure(
                    $"Google returned non-audio response ({mediaType ?? "none"}). Preview: {preview}");
            }

            var mp3Bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            var tempPath = Path.Combine(Path.GetTempPath(), $"qt_{Guid.NewGuid():N}.mp3");
            try
            {
                await File.WriteAllBytesAsync(tempPath, mp3Bytes, cancellationToken);

                using var reader = new MediaFoundationReader(tempPath);
                var fmt = reader.WaveFormat;
                player.Initialize(fmt.SampleRate, fmt.Channels, fmt.BitsPerSample);

                var pcmBuffer = new byte[reader.Length];
                int totalRead = 0;
                while (totalRead < pcmBuffer.Length)
                {
                    int read = reader.Read(pcmBuffer, totalRead,
                        (int)Math.Min(pcmBuffer.Length - totalRead, 8192));
                    if (read == 0) break;
                    totalRead += read;
                }

                if (totalRead > 0)
                {
                    var actualPcm = totalRead < pcmBuffer.Length ? pcmBuffer[..totalRead] : pcmBuffer;
                    await player.EnqueueSamplesAsync(actualPcm, cancellationToken);
                    player.RecordChunkBoundary();
                    player.Play();
                }

                return PronunciationResult<bool>.Success(true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (OperationCanceledException)
        {
            return PronunciationResult<bool>.Success(true);
        }
        catch (HttpRequestException ex)
        {
            return PronunciationResult<bool>.Failure($"Network error connecting to Google: {ex.Message}", ex);
        }
        catch (EndOfStreamException)
        {
            // Reached end of streaming response abruptly
            player.Play();
            return PronunciationResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return PronunciationResult<bool>.Failure($"Google streaming failed: {ex.GetType().Name}: {ex.Message}", ex);
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

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
