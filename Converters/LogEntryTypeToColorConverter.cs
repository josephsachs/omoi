using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Omoi.Models;

namespace Omoi.Converters;

public class LogEntryTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogEntryType.Error)
            return new SolidColorBrush(Color.Parse("#FF6B6B"));
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
