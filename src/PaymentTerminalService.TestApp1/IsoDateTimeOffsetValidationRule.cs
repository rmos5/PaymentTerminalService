using System;
using System.Globalization;
using System.Windows.Controls;

namespace PaymentTerminalService.TestApp1
{
    public class IsoDateTimeOffsetValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string input = value as string;
            if (string.IsNullOrWhiteSpace(input))
                return new ValidationResult(false, "Timestamp is required.");

            // Accept both UTC (Z) and offset (+HH:mm)
            string[] formats = {
                "yyyy-MM-ddTHH:mm:ssZ",
                "yyyy-MM-ddTHH:mm:sszzz"
            };

            if (DateTimeOffset.TryParseExact(
                input,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
            {
                return ValidationResult.ValidResult;
            }
            return new ValidationResult(false, "Invalid ISO 8601 timestamp (e.g. 2024-06-24T15:30:00Z or 2024-06-24T15:30:00+02:00).");
        }
    }
}