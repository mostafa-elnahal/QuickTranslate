namespace QuickTranslate.Services.Audio;

/// <summary>
/// Default factory that creates <see cref="NAudioStreamingPlayer"/> instances.
/// </summary>
public class StreamingAudioPlayerFactory : IStreamingAudioPlayerFactory
{
    /// <inheritdoc />
    public IStreamingAudioPlayer CreatePlayer() => new NAudioStreamingPlayer();
}
