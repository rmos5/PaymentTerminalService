using System;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Defines the contract for a payment terminal, including capabilities, configuration, status, operations, and resource management.
    /// Implementations must provide all runtime behaviors and state for a specific terminal and its connection.
    /// </summary>
    public interface IPaymentTerminal : IDisposable
    {
        /// <summary>
        /// Gets the unique terminal identifier.
        /// This value corresponds to the terminalId property in the OpenAPI specification and is used to uniquely identify the terminal instance across API, model, and implementation layers.
        /// </summary>
        string TerminalId { get; }

        /// <summary>
        /// Gets the vendor name of the terminal.
        /// </summary>
        string Vendor { get; }

        /// <summary>
        /// Gets the model identifier of the terminal.
        /// </summary>
        string Model { get; }

        /// <summary>
        /// Gets the firmware or software version of the terminal.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the display name of the terminal.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets a value indicating whether loyalty operations are supported by this terminal.
        /// </summary>
        bool IsLoyaltySupported { get; }

        /// <summary>
        /// Gets a value indicating whether reversal operations are supported by this terminal.
        /// </summary>
        bool IsReversalSupported { get; }

        /// <summary>
        /// Gets a value indicating whether refund operations are supported by this terminal.
        /// </summary>
        bool IsRefundSupported { get; }

        /// <summary>
        /// Gets the selected connection option used to activate this terminal.
        /// </summary>
        TerminalConnectionOption Connection { get; }

        /// <summary>
        /// Gets the vendor-specific static metadata snapshot.
        /// </summary>
        VendorPayload VendorPayload { get; }

        /// <summary>
        /// Aborts the current operation, if possible.
        /// </summary>
        /// <param name="request">
        /// The abort request. When <see cref="AbortTransactionRequest.Force"/> is <see langword="false"/>,
        /// sends the abort command to the terminal and polls for result. When <see langword="true"/>,
        /// closes the server-side session immediately without sending an abort command —
        /// the outcome is recorded as unknown and queued for background reconciliation.
        /// </param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        Task<OperationAccepted> AbortTransactionAsync(AbortTransactionRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current settings for the terminal.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The current terminal settings.</returns>
        Task<TerminalSettings> GetTerminalSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the latest runtime status of the terminal.
        /// Returns a single <see cref="TerminalStatus"/> object representing the current operational state, active operation, prompts, results, and any faults.
        /// Suitable for polling scenarios where only the most recent status is required.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>
        /// The latest <see cref="TerminalStatus"/> for the terminal.
        /// </returns>
        Task<TerminalStatus> GetTerminalStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the status history for the current terminal session.
        /// Returns a <see cref="TerminalSessionResponse"/> containing an ordered list of <see cref="TerminalStatus"/> objects,
        /// with the most recent status first. Use this method to obtain all status updates for the active session,
        /// supporting workflow tracking, diagnostics, or session-based reporting.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>
        /// A <see cref="TerminalSessionResponse"/> containing all status results for the current session, ordered from most recent to oldest.
        /// </returns>
        Task<TerminalSessionResponse> GetTerminalSessionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a loyalty activation operation on the terminal.
        /// </summary>
        /// <param name="request">The loyalty activation request details.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        Task<OperationAccepted> LoyaltyActivateAsync(LoyaltyActivateRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a loyalty deactivation operation on the terminal.
        /// </summary>
        /// <param name="request">The loyalty deactivation request details.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        Task<OperationAccepted> LoyaltyDeactivateAsync(BaseActionRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a purchase operation on the terminal.
        /// </summary>
        /// <param name="request">The purchase request details.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        Task<OperationAccepted> StartPurchaseAsync(PurchaseRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a refund operation on the terminal.
        /// </summary>
        /// <param name="request">The refund request details.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        Task<OperationAccepted> StartRefundAsync(RefundRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts a reversal operation on the terminal.
        /// </summary>
        /// <param name="request">The reversal request details.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        Task<OperationAccepted> StartReversalAsync(ReversalRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Responds to an active prompt on the terminal.
        /// </summary>
        /// <param name="request">The prompt response request details.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        Task<OperationAccepted> RespondToPromptAsync(PromptResponseRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Notifies the service that the current transaction has been processed by the calling entity.
        /// </summary>
        /// <param name="request">The confirmation request, indicating whether the transaction was accepted or rejected by the caller.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        Task<OperationAccepted> ConfirmTransactionAsync(TransactionConfirmRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases or deactivates the terminal, freeing any held resources.
        /// </summary>
        /// <returns>The accepted operation response.</returns>
        Task<OperationAccepted> ReleaseAsync();

        /// <summary>
        /// Starts a connectivity test for the terminal.
        /// This method requests verification that the terminal is reachable and operational
        /// without starting a transaction or other business operation.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>
        /// An <see cref="OperationAccepted"/> response containing the accepted connectivity test operation.
        /// </returns>
        Task<OperationAccepted> TestConnectionAsync(CancellationToken cancellationToken = default);
    }
}