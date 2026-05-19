using System;
using System.Threading;
using System.Threading.Tasks;
using QuickTranslate.Models;
using QuickTranslate.Helpers;
using QuickTranslate.Services.Audio;
using System.Net.Http;
using System.IO;

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

            using var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            
            NAudio.Wave.IMp3FrameDecompressor? decompressor = null;
            try
            {
                NAudio.Wave.Mp3Frame? frame;
                while ((frame = NAudio.Wave.Mp3Frame.LoadFromStream(networkStream)) != null)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    if (decompressor == null)
                    {
                        var waveFormat = new NAudio.Wave.Mp3WaveFormat(
                            frame.SampleRate, frame.ChannelMode == NAudio.Wave.ChannelMode.Mono ? 1 : 2,
                            frame.FrameLength, frame.BitRate);
                        decompressor = new NAudio.Wave.AcmMp3FrameDecompressor(waveFormat);
                        
                        player.Initialize(frame.SampleRate, frame.ChannelMode == NAudio.Wave.ChannelMode.Mono ? 1 : 2, 16);
                    }

                    byte[] pcmBuffer = new byte[decompressor.OutputFormat.AverageBytesPerSecond];
                    int bytesDecompressed = decompressor.DecompressFrame(frame, pcmBuffer, 0);
                    if (bytesDecompressed > 0)
                    {
                        byte[] pcmChunk = new byte[bytesDecompressed];
                        Buffer.BlockCopy(pcmBuffer, 0, pcmChunk, 0, bytesDecompressed);
                        await player.EnqueueSamplesAsync(pcmChunk, cancellationToken);
                    }
                }
            }
            finally
            {
                decompressor?.Dispose();
            }

            player.Play();
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
            return PronunciationResult<bool>.Failure("Google streaming failed.", ex);
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
