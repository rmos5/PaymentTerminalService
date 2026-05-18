namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Provides monetary unit conversion helpers for use across terminal implementations.
    /// </summary>
    public static class MoneyConversions
    {
        /// <summary>
        /// Converts a monetary amount from minor units (e.g. 1210 = 12.10 EUR) to a decimal
        /// value as expected by terminal APIs.
        /// </summary>
        /// <param name="amountInMinorUnits">Amount in minor units (e.g. cents).</param>
        /// <param name="currency">
        /// ISO 4217 currency code. Currently unused — all supported currencies use 2 decimal places.
        /// Extend with a switch when non-standard currencies (e.g. JPY with 0 decimals) are required.
        /// </param>
        /// <returns>Decimal amount suitable for passing to the terminal API.</returns>
        public static decimal MinorUnitsToDecimal(long amountInMinorUnits, string currency = null)
        {
            return amountInMinorUnits / 100m;
        }
    }
}