namespace QuickTranslate.Models;

/// <summary>
/// Represents a single word-level audio timepoint from a pronunciation provider.
/// </summary>
/// <param name="MarkName">The mark name (e.g., "w0", "w1").</param>
/// <param name="TimeSeconds">The time in seconds when this mark is reached during playback.</param>
public record TimepointInfo(string MarkName, double TimeSeconds);
