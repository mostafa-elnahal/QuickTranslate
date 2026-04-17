using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuickTranslate.Models;

/// <summary>
/// Model for displaying a translation provider in the UI.
/// </summary>
public class ProviderInfo : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _brandColor = "#888888";
    private bool _isSelected;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string BrandColor
    {
        get => _brandColor;
        set
        {
            if (_brandColor != value)
            {
                _brandColor = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Creates provider info with brand colors for known providers.
    /// </summary>
    public static ProviderInfo Create(string providerName, bool isSelected = false)
    {
        var brandColor = providerName switch
        {
            "Google" => "#4285F4",
            "Bing" => "#00A4EF",
            "Microsoft" => "#737373",
            "Yandex" => "#FF0000",
            _ => "#888888"
        };

        return new ProviderInfo
        {
            Name = providerName,
            BrandColor = brandColor,
            IsSelected = isSelected
        };
    }
}
