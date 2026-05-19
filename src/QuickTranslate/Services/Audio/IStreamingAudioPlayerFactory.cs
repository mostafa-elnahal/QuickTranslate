namespace QuickTranslate.Services.Audio;

/// <summary>
/// Factory for creating <see cref="IStreamingAudioPlayer"/> instances.
/// Enables DI-friendly creation of per-session audio players.
/// </summary>
public interface IStreamingAudioPlayerFactory
{
    /// <summary>
    /// Creates a new <see cref="IStreamingAudioPlayer"/> instance.
    /// </summary>
    IStreamingAudioPlayer CreatePlayer();
}
