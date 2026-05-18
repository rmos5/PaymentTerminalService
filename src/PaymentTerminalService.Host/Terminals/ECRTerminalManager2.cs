using System.Diagnostics;
using Verifone.ECRTerminal;

namespace PaymentTerminalService.Terminals
{
    /// <summary>
    /// Extends <see cref="ECRTerminalManager"/> with runtime-configurable authorization policies
    /// read from the terminal's <c>VendorPayload</c> at activation time.
    /// </summary>
    internal sealed class ECRTerminalManager2 : ECRTerminalManager
    {
        /// <summary>
        /// Gets a value indicating whether manual authorization flows (result codes 2003, 2007)
        /// are permitted. Configured via <c>allowManualAuthorization</c> in VendorPayload.
        /// </summary>
        public bool EnableManualAuthorization { get; }

        public ECRTerminalManager2(
            string portName,
            IUserPromptHandler userPromptHandler,
            bool enableManualAuthorization,
            bool traceSerialBytes = false)
            : base(portName, userPromptHandler, traceSerialBytes: traceSerialBytes)
        {
            EnableManualAuthorization = enableManualAuthorization;
        }

        /// <inheritdoc/>
        protected override bool ShouldAllowManualAuthorization(string resultCode)
        {
            Trace.WriteLine($"{nameof(ShouldAllowManualAuthorization)}: resultCode={resultCode} enabled={EnableManualAuthorization}", GetType().FullName);
            return EnableManualAuthorization;
        }
    }
}