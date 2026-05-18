namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Well-known <see cref="VendorPayload.AdditionalProperties"/> key constants
    /// recognized across all terminal implementations at the model layer.
    /// </summary>
    public static class VendorPayloadKeys
    {
        /// <summary>
        /// Abort timeout in seconds before a session is force-closed with unknown outcome.
        /// </summary>
        public const string AbortTimeoutSeconds = "abortTimeoutSeconds";

        /// <summary>
        /// Prompt response timeout in seconds before a pending prompt is auto-declined.
        /// </summary>
        public const string PromptTimeoutSeconds = "promptTimeoutSeconds";

        /// <summary>
        /// Storage key identifying the persisted session file name.
        /// </summary>
        public const string SessionName = "sessionName";

        /// <summary>
        /// Controls whether refund operations are supported for the terminal.
        /// </summary>
        public const string IsRefundSupported = "isRefundSupported";

        /// <summary>
        /// Controls whether reversal operations are supported for the terminal.
        /// </summary>
        public const string IsReversalSupported = "isReversalSupported";

        /// <summary>
        /// Terminal-assigned transaction identifier. Present in intermediate statuses
        /// (e.g. <c>TransactionInitialized</c>) and in final transaction result payloads.
        /// </summary>
        public const string TransactionId = "transactionId";

        /// <summary>
        /// Date and time of the transaction as reported by the terminal.
        /// Present in intermediate statuses (e.g. <c>TransactionInitialized</c>)
        /// and in final transaction result payloads.
        /// </summary>
        public const string TransactionDateTime = "transactionDateTime";
    }
}