using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickTranslate.Converters;

/// <summary>
/// Converts a category string to Visibility based on whether it matches the parameter.
/// </summary>
public class CategoryToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string selectedCategory && parameter is string targetCategory)
        {
            return selectedCategory == targetCategory ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
