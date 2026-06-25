using System;
using System.Globalization;
using System.Windows.Data;
using QuickTranslate.Models;

namespace QuickTranslate.Converters;

public class LanguageDisplayConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 3 &&
            values[0] is LanguageOption option &&
            values[1] is bool autoDetect &&
            values[2] is string detectedLang)
        {
            if (autoDetect && option.Code == "auto" && !string.IsNullOrEmpty(detectedLang))
                return detectedLang;
        }

        if (values.Length >= 1 && values[0] is LanguageOption opt)
            return opt.DisplayName;

        return "Auto-detect";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
