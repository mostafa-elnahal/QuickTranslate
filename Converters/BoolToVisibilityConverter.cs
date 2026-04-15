using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickTranslate.Converters;

/// <summary>
/// Converts a boolean value to Visibility.
/// True = Visible, False = Collapsed.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Support inversion via parameter
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            return (visibility == Visibility.Visible) ^ invert;
        }
        return false;
    }
}
