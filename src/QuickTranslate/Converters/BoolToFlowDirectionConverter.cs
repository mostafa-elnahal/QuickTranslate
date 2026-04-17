using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickTranslate.Converters;

/// <summary>
/// Converts a boolean IsRtl value to FlowDirection.
/// true = RightToLeft, false = LeftToRight
/// </summary>
public class BoolToFlowDirectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isRtl && isRtl)
        {
            return FlowDirection.RightToLeft;
        }
        return FlowDirection.LeftToRight;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
