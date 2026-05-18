namespace PaymentTerminalService.Terminals
{
    /// <summary>
    /// Well-known <see cref="VendorPayload.AdditionalProperties"/> key constants
    /// specific to the <see cref="VerifoneYomaniXRTerminal"/> implementation.
    /// </summary>
    internal static class VerifoneYomaniXRVendorPayloadKeys
    {
        /// <summary>
        /// When <c>true</c>, raw serial bytes are written to <see cref="System.Diagnostics.Trace"/> output.
        /// </summary>
        public const string TraceSerialBytes = "traceSerialBytes";

        /// <summary>
        /// When <c>true</c>, the terminal may raise a free-form input prompt during manual authorization.
        /// </summary>
        public const string EnableManualAuthorization = "enableManualAuthorization";
    }
}