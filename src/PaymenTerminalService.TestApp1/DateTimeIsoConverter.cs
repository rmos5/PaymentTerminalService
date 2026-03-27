using System;
using System.Globalization;
using System.Windows.Data;

namespace PaymentTerminalService.TestApp1
{
    public class DateTimeIsoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
                return dt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && DateTime.TryParseExact(
                s,
                "yyyy-MM-ddTHH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dt))
            {
                return dt;
            }
            return Binding.DoNothing;
        }
    }
}