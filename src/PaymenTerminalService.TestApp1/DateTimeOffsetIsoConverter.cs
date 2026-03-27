using System;
using System.Globalization;
using System.Windows.Data;

namespace PaymentTerminalService.TestApp1
{
    public class DateTimeOffsetIsoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTimeOffset dto)
                return dto.ToString("yyyy-MM-ddTHH:mm:ssZ");
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && DateTimeOffset.TryParseExact(
                s,
                "yyyy-MM-ddTHH:mm:ssZ",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
            {
                return dto;
            }
            return Binding.DoNothing;
        }
    }
}