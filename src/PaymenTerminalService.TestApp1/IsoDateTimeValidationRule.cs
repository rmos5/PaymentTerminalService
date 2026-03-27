using System;
using System.Globalization;
using System.Windows.Controls;

namespace PaymentTerminalService.TestApp1
{
    public class IsoDateTimeValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string input = value as string;
            if (string.IsNullOrWhiteSpace(input))
                return new ValidationResult(false, "Timestamp is required.");

            if (DateTime.TryParseExact(
                input,
                "yyyy-MM-ddTHH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
            {
                return ValidationResult.ValidResult;
            }
            return new ValidationResult(false, "Invalid timestamp format (e.g. 2024-06-24T15:30:00).");
        }
    }
}