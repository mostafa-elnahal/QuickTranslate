using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickTranslate.Models;

public class SyllableItem : INotifyPropertyChanged
{
    private bool _isActive;
    private string _text = string.Empty;

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

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isStressed;
    public bool IsStressed
    {
        get => _isStressed;
        set
        {
            if (_isStressed != value)
            {
                _isStressed = value;
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
