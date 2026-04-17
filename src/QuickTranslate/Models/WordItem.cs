using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickTranslate.Models;

/// <summary>
/// Represents a single word in the pronunciation display with highlight state.
/// Used for hybrid karaoke-style word highlighting during audio playback.
/// it inherits from INotifyPropertyChanged to notify the UI of changes like IsInActiveChunk and IsActiveWord
/// </summary>
public class WordItem : INotifyPropertyChanged
{
    private string _text = string.Empty;
    private bool _isInActiveChunk;
    private bool _isActiveWord;

    /// <summary>
    /// The word text (including trailing space if applicable).
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether this word belongs to the chunk currently being spoken.
    /// Drives the background highlight.
    /// </summary>
    public bool IsInActiveChunk
    {
        get => _isInActiveChunk;
        set
        {
            if (_isInActiveChunk != value)
            {
                _isInActiveChunk = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether this is the specific word currently being spoken.
    /// Drives the foreground accent color highlight.
    /// </summary>
    public bool IsActiveWord
    {
        get => _isActiveWord;
        set
        {
            if (_isActiveWord != value)
            {
                _isActiveWord = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
