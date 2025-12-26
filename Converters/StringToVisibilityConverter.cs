using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickTranslate.Converters;

/// <summary>
/// Converts string values to WPF Visibility enumeration.
/// </summary>
/// <remarks>
/// This converter is used to conditionally show/hide UI elements based on string content.
/// 
/// <para>Usage Example:</para>
/// <code>
/// &lt;TextBlock Visibility="{Binding SynonymsText, Converter={StaticResource StringToVis}}"/&gt;
/// </code>
/// 
/// <para>Behavior:</para>
/// <list type="bullet">
/// <item>Returns <see cref="Visibility.Visible"/> when string is not null and contains non-whitespace content</item>
/// <item>Returns <see cref="Visibility.Collapsed"/> when string is null, empty, or contains only whitespace</item>
/// </list>
/// 
/// <para>Used in: Views/PopupWindow.xaml for displaying synonym text elements dynamically</para>
/// </remarks>
public class StringToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a string value to Visibility.
    /// </summary>
    /// <param name="value">The string value to convert. Can be null or any type.</param>
    /// <param name="targetType">The target type (should be <see cref="Visibility"/>).</param>
    /// <param name="parameter">Optional parameter (not used in this converter).</param>
    /// <param name="culture">The culture information (not used in this converter).</param>
    /// <returns>
    /// <see cref="Visibility.Visible"/> if the value is a non-null string with non-whitespace content,
    /// otherwise <see cref="Visibility.Collapsed"/>.
    /// </returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Check if value is a string and contains non-whitespace content
        if (value is string text && !string.IsNullOrWhiteSpace(text))
        {
            // String has content - show the UI element
            return Visibility.Visible;
        }
        // String is null, empty, or whitespace - hide the UI element
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
