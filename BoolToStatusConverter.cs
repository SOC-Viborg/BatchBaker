using System;
using System.Globalization;
using System.Windows.Data;

namespace BatchBaker
{
    // Displays ImportSuccess as "Valid"/"Invalid" instead of the raw True/False.
    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool success && success ? "Valid" : "Invalid";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
