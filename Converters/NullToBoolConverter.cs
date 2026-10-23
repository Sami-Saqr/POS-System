using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CarPartManager.Converters
{
    /// <summary>
    /// Converts null to true, non-null to false (for IsEnabled when checking if object is null)
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
