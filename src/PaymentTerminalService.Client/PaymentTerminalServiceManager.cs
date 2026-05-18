using PaymentTerminalService.Model;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace PaymentTerminalService.Client
{
    /// <summary>
    /// Provides a high-level, stateful manager for interacting with the payment terminal service.
    /// Wraps <see cref="PaymentTerminalServiceClient"/> and manages terminal status polling internally.
    /// Polling starts automatically after each transaction or action method and stops when a final
    /// status is received or when the manager is disposed.
    /// </summary>
    public class PaymentTerminalServiceManager : IDisposable
    {
        public const int DefaultPollingIntervalSeconds = 3;
        public const int DefaultPollingStartDelaySeconds = 1;

        private readonly PaymentTerminalServiceClient client;
        private readonly HttpClient httpClient;
        private readonly TimeSpan pollingInterval;
        private readonly TimeSpan pollingStartDelay;
        private readonly TerminalStatusPoller poller;
        private bool disposed;

        /// <summary>
        /// Occurs when a new terminal status is received from polling.
        /// </summary>
        /// <remarks>
        /// This event fires for every polled status, both intermediate and final.
        /// For the final event in a polling cycle, <see cref="TerminalStatusEventArgs.IsLastPoll"/>
        /// is <see langword="true"/> and <see cref="TerminalStatusEventArgs.StopReason"/> describes
        /// why polling ended.
        /// <para>
        /// To handle specific operation outcomes, check <see cref="TerminalStatus.LastResultIsFinal"/>
        /// and <see cref="TerminalStatus.ActiveOperationType"/> on the received status:
        /// <code>
        /// manager.StatusReceived += (s, e) =>
        /// {
        ///     if (e.Status == null || !e.Status.LastResultIsFinal) return;
        ///     switch (e.Status.ActiveOperationType)
        ///     {
        ///         case OperationType.Purchase: /* handle purchase result */ break;
        ///         case OperationType.Refund:   /* handle refund result   */ break;
        ///         case OperationType.Reversal: /* handle reversal result */ break;
        ///         case OperationType.Abort:    /* handle abort result    */ break;
        ///     }
        /// };
        /// </code>
        /// </para>
        /// </remarks>
        public event EventHandler<TerminalStatusEventArgs> StatusReceived;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentTerminalServiceManager"/> class
        /// with an explicit base URL. When provided, overrides the application configuration value.
        /// </summary>
        /// <param name="baseUrl">
        /// The base URL of the payment terminal service, or <see langword="null"/> to resolve
        /// from application configuration.
        /// </param>
        /// <param name="pollingIntervalSeconds">Polling interval in seconds. Defaults to <see cref="DefaultPollingIntervalSeconds"/>.</param>
        /// <param name="pollingStartDelaySeconds">Delay in seconds before the first poll after starting. Defaults to <see cref="DefaultPollingStartDelaySeconds"/>.</param>
        public PaymentTerminalServiceManager(
            string baseUrl,
            int pollingIntervalSeconds = DefaultPollingIntervalSeconds,
            int pollingStartDelaySeconds = DefaultPollingStartDelaySeconds)
        {
            Trace.WriteLine($"{nameof(PaymentTerminalServiceManager)}: baseUrl={baseUrl ?? "(from config)"}", GetType().FullName);

            if (baseUrl != null && !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
                throw new ArgumentException("baseUrl must be a valid absolute URL.", nameof(baseUrl));

            pollingInterval = TimeSpan.FromSeconds(pollingIntervalSeconds);
            pollingStartDelay = TimeSpan.FromSeconds(pollingStartDelaySeconds);

            httpClient = new HttpClient();
            client = new PaymentTerminalServiceClient(httpClient);
            client.BaseUrl = baseUrl;

            poller = new TerminalStatusPoller(client.GetSelectedTerminalStatusAsync, pollingInterval);
            poller.StatusReceived += Poller_StatusReceived;
        }

        private void Poller_StatusReceived(object sender, TerminalStatusEventArgs e)
        {
            // Suppress the final event from an internal polling restart — it carries stale
            // status from before the restart and would cause the consumer to re-process it.
            // A restart stop is not a meaningful event; the next poll will deliver the fresh status.
            if (e.IsLastPoll && e.StopReason == TerminalStatusPollStopReason.Stopped
                && e.Status != null && !e.Status.LastResultIsFinal)
            {
                return;
            }

            StatusReceived?.Invoke(this, e);

            if (e.Status != null && e.Status.LastResultIsFinal)
            {
                poller.Stop();
            }
        }

        private void StartPolling()
        {
            var startDelay = pollingStartDelay > TimeSpan.Zero
                ? (TimeSpan?)pollingStartDelay
                : null;

            poller.Start(startDelay);
        }

        private void RestartPollingWithDelay()
        {
            poller.Start(pollingStartDelay > TimeSpan.Zero ? (TimeSpan?)pollingStartDelay : null);
        }

        private void StopAndRestartPolling()
        {
            poller.Stop();
            StartPolling();
        }

        /// <summary>
        /// Retrieves all available terminals and the currently selected terminal from the service.
        /// </summary>
        /// <returns>The terminal catalog response.</returns>
        public Task<TerminalCatalogResponse> GetTerminalsAsync()
        {
            Trace.WriteLine($"{nameof(GetTerminalsAsync)}", GetType().FullName);

            return client.GetTerminalsAsync();
        }

        /// <summary>
        /// Selects a terminal and connection on the service.
        /// </summary>
        /// <param name="request">The terminal selection request.</param>
        /// <returns>The selected terminal response.</returns>
        public Task<SelectedTerminalResponse> SelectTerminalAsync(SelectTerminalRequest request)
        {
            Trace.WriteLine($"{nameof(SelectTerminalAsync)}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return client.SelectTerminalAsync(request);
        }

        /// <summary>
        /// Retrieves the settings for the currently selected terminal.
        /// </summary>
        /// <returns>The terminal settings.</returns>
        public Task<TerminalSettings> GetSelectedTerminalSettingsAsync()
        {
            Trace.WriteLine($"{nameof(GetSelectedTerminalSettingsAsync)}", GetType().FullName);

            return client.GetSelectedTerminalSettingsAsync();
        }

        /// <summary>
        /// Retrieves the latest status for the currently selected terminal.
        /// This is a one-shot query and does not affect the active polling cycle.
        /// </summary>
        /// <returns>The terminal status.</returns>
        public Task<TerminalStatus> GetSelectedTerminalStatusAsync()
        {
            Trace.WriteLine($"{nameof(GetSelectedTerminalStatusAsync)}", GetType().FullName);

            return client.GetSelectedTerminalStatusAsync();
        }

        /// <summary>
        /// Retrieves the current session information for the selected terminal.
        /// </summary>
        /// <returns>The terminal session response.</returns>
        public Task<TerminalSessionResponse> GetSelectedTerminalSessionAsync()
        {
            Trace.WriteLine($"{nameof(GetSelectedTerminalSessionAsync)}", GetType().FullName);

            return client.GetSelectedTerminalSessionAsync();
        }

        /// <summary>
        /// Starts a purchase transaction on the selected terminal and begins polling for status.
        /// </summary>
        /// <param name="request">The purchase request.</param>
        /// <returns>The accepted operation descriptor.</returns>
        public async Task<OperationAccepted> StartPurchaseAsync(PurchaseRequest request)
        {
            Trace.WriteLine($"{nameof(StartPurchaseAsync)}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            poller.Stop();
            try
            {
                OperationAccepted result = await client.StartPurchaseAsync(request).ConfigureAwait(false);
                StartPolling();
                return result;
            }
            catch
            {
                StartPolling();
                throw;
            }
        }

        /// <summary>
        /// Starts a reversal transaction on the selected terminal and begins polling for status.
        /// </summary>
        /// <param name="request">The reversal request.</param>
        /// <returns>The accepted operation descriptor.</returns>
        public async Task<OperationAccepted> StartReversalAsync(ReversalRequest request)
        {
            Trace.WriteLine($"{nameof(StartReversalAsync)}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            poller.Stop();
            try
            {
                OperationAccepted result = await client.StartReversalAsync(request).ConfigureAwait(false);
                StartPolling();
                return result;
            }
            catch
            {
                StartPolling();
                throw;
            }
        }

        /// <summary>
        /// Starts a refund transaction on the selected terminal and begins polling for status.
        /// </summary>
        /// <param name="request">The refund request.</param>
        /// <returns>The accepted operation descriptor.</returns>
        public async Task<OperationAccepted> StartRefundAsync(RefundRequest request)
        {
            Trace.WriteLine($"{nameof(StartRefundAsync)}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            poller.Stop();
            try
            {
                OperationAccepted result = await client.StartRefundAsync(request).ConfigureAwait(false);
                StartPolling();
                return result;
            }
            catch
            {
                StartPolling();
                throw;
            }
        }

        /// <summary>
        /// Aborts the current transaction on the selected terminal and resumes polling for status.
        /// Polling is restarted only if no final status has been received yet; if the abort timeout
        /// has already closed the session with a final result, polling is left stopped.
        /// </summary>
        /// <param name="request">The abort transaction request.</param>
        /// <returns>The accepted operation descriptor.</returns>
        public async Task<OperationAccepted> AbortTransactionAsync(AbortTransactionRequest request)
        {
            Trace.WriteLine($"{nameof(AbortTransactionAsync)}:{request}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var latest = poller.LatestStatus;
            if (latest == null || !latest.LastResultIsFinal)
                StopAndRestartPolling();

            return await client.AbortTransactionAsync(request).ConfigureAwait(false);
        }

        /// <summary>
        /// Activates a loyalty detection session on the selected terminal and begins polling for status.
        /// Unlike transaction methods, polling is not stopped before the request is sent and is not
        /// restarted on failure; the caller is responsible for managing polling state if the request throws.
        /// </summary>
        /// <param name="request">The loyalty activation request.</param>
        /// <returns>The accepted operation descriptor.</returns>
        public async Task<OperationAccepted> LoyaltyActivateAsync(LoyaltyActivateRequest request)
        {
            Trace.WriteLine($"{nameof(LoyaltyActivateAsync)}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            OperationAccepted result = await client.LoyaltyActivateAsync(request).ConfigureAwait(false);
            StartPolling();
            return result;
        }

        /// <summary>
        /// Deactivates the loyalty detection session on the selected terminal and begins polling for status.
        /// </summary>
        /// <param name="request">The loyalty deactivation request.</param>
        /// <returns>The accepted operation descriptor.</returns>
        public async Task<OperationAccepted> LoyaltyDeactivateAsync(BaseActionRequest request)
        {
            Trace.WriteLine($"{nameof(LoyaltyDeactivateAsync)}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            poller.Stop();
            try
            {
                OperationAccepted result = await client.LoyaltyDeactivateAsync(request).ConfigureAwait(false);
                StartPolling();
                return result;
            }
            catch
            {
                StartPolling();
                throw;
            }
        }

        /// <summary>
        /// Responds to an active prompt on the selected terminal.
        /// Polling is restarted with the configured start delay after the response is sent,
        /// allowing server-side state to advance before the next poll.
        /// </summary>
        /// <param name="request">The prompt response request.</param>
        /// <returns>The accepted operation descriptor.</returns>
        public async Task<OperationAccepted> RespondToPromptAsync(PromptResponseRequest request)
        {
            Trace.WriteLine($"{nameof(RespondToPromptAsync)}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            OperationAccepted result = await client.RespondToPromptAsync(request).ConfigureAwait(false);
            RestartPollingWithDelay();
            return result;
        }

        /// <summary>
        /// Notifies the service that the transaction result has been processed by the caller.
        /// Must be called explicitly by the consumer after receiving a final terminal status.
        /// Polling is stopped after the confirmation is sent.
        /// </summary>
        /// <param name="request">The transaction confirmation request.</param>
        /// <returns>The accepted operation descriptor.</returns>
        public async Task<OperationAccepted> ConfirmTransactionAsync(TransactionConfirmRequest request)
        {
            Trace.WriteLine($"{nameof(ConfirmTransactionAsync)}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            OperationAccepted result = await client.ConfirmTransactionAsync(request).ConfigureAwait(false);
            poller.Stop();
            return result;
        }

        /// <summary>
        /// Disposes the manager, stops polling, and releases all resources.
        /// </summary>
        public void Dispose()
        {
            Trace.WriteLine($"{nameof(Dispose)}", GetType().FullName);

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the resources used by the manager.
        /// </summary>
        /// <param name="disposing">True to dispose managed resources; otherwise, false.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            disposed = true;

            if (disposing)
            {
                poller.StatusReceived -= Poller_StatusReceived;
                poller.Dispose();
                httpClient.Dispose();
            }
        }
    }
}