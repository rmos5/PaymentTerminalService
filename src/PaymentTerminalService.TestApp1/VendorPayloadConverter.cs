using System;
using System.Globalization;
using System.Windows.Data;
using Newtonsoft.Json;
using PaymentTerminalService.Model;

namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Converts VendorPayload (or its dictionary) to formatted JSON string for display in WPF controls.
    /// </summary>
    public class VendorPayloadConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            // Try to get dictionary from VendorPayload or directly
            var dict = value as System.Collections.Generic.IDictionary<string, object>;
            if (dict == null)
            {
                var additionalPropsProp = value.GetType().GetProperty("AdditionalProperties");
                if (additionalPropsProp != null)
                {
                    dict = additionalPropsProp.GetValue(value) as System.Collections.Generic.IDictionary<string, object>;
                }
            }

            if (dict != null)
            {
                return JsonConvert.SerializeObject(dict, Formatting.Indented);
            }

            // Fallback: serialize the whole object
            return JsonConvert.SerializeObject(value, Formatting.Indented);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var json = value as string;
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<VendorPayload>(json);
            }
            catch
            {
                // Optionally log or handle the error
                return null;
            }
        }
    }
}
