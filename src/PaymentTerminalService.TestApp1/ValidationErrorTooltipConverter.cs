using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace PaymentTerminalService.TestApp1
{
    public class ValidationErrorTooltipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var errors = values[0] as System.Collections.ObjectModel.ReadOnlyCollection<ValidationError>;
            if (errors != null && errors.Count > 0)
                return errors[0].ErrorContent?.ToString();
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}