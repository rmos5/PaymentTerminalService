using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Provides a base abstract implementation of the <see cref="IPaymentTerminal"/> interface.
    /// Inherit from this class to implement a specific payment terminal, supplying all required operations and properties.
    /// </summary>
    public abstract partial class PaymentTerminalBase : IPaymentTerminal
    {
        /// <summary>
        /// Default version string used when no version is specified.
        /// </summary>
        public const string DefaultVersionString = "NA";

        private bool disposed;

        /// <inheritdoc/>
        public string TerminalId { get; protected set; }

        /// <inheritdoc/>
        public abstract string Vendor { get; }

        /// <inheritdoc/>
        public abstract string Model { get; }

        /// <inheritdoc/>
        public virtual string Version { get; protected set; } = DefaultVersionString;

        /// <inheritdoc/>
        public string DisplayName { get; }

        /// <inheritdoc/>
        public TerminalConnectionOption Connection { get; private set; }

        /// <inheritdoc/>
        public bool IsLoyaltySupported { get; protected set; }

        /// <inheritdoc/>
        public bool IsRefundSupported { get; private set; }

        /// <inheritdoc/>
        public bool IsReversalSupported { get; private set; }

        /// <inheritdoc/>
        public VendorPayload VendorPayload { get; private set; }

        /// <summary>
        /// Gets the session storage provider used to persist and load terminal session data.
        /// </summary>
        protected ISessionStorageProvider SessionStorageProvider { get; }

        /// <summary>
        /// Gets a value indicating whether the terminal device information has been updated.
        /// </summary>
        protected bool IsDeviceInfoUpdated { get; private set; } = false;

        /// <summary>
        /// Gets the abort timeout in seconds. When abort retries exceed this duration the session
        /// is closed with an <c>AbortTimeout</c> fault and the outcome is marked as unknown.
        /// Configured via <c>abortTimeoutSeconds</c> in the constructor <see cref="VendorPayload"/>.
        /// </summary>
        protected int AbortTimeoutSeconds { get; }

        /// <summary>
        /// Gets the prompt response timeout in seconds.
        /// When elapsed, the pending prompt is auto-declined and the transaction aborted.
        /// Configured via <c>promptTimeoutSeconds</c> in the constructor <see cref="VendorPayload"/>.
        /// </summary>
        protected int PromptTimeoutSeconds { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentTerminalBase"/> class.
        /// </summary>
        /// <param name="terminalId">Unique identifier for the terminal.</param>
        /// <param name="displayName">Display name for the terminal.</param>
        /// <param name="connection">Connection options for the terminal.</param>
        /// <param name="isLoyaltySupported">Indicates whether loyalty is supported.</param>
        /// <param name="vendorPayload">Vendor-specific item data.</param>
        /// <param name="sessionStorageProvider">Session storage provider used for persisting session data.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="terminalId"/>, <paramref name="displayName"/>, <paramref name="connection"/>, or <paramref name="sessionStorageProvider"/> is invalid.</exception>
        protected PaymentTerminalBase(
            string terminalId,
            string displayName,
            TerminalConnectionOption connection,
            bool isLoyaltySupported,
            VendorPayload vendorPayload,
            ISessionStorageProvider sessionStorageProvider)
        {
            if (string.IsNullOrWhiteSpace(terminalId))
                throw new ArgumentNullException(nameof(terminalId), "TerminalId must not be null or empty.");
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentNullException(nameof(displayName), "DisplayName must not be null or empty.");
            if (connection == null)
                throw new ArgumentNullException(nameof(connection), "Connection must not be null.");
            if (sessionStorageProvider == null)
                throw new ArgumentNullException(nameof(sessionStorageProvider), "Session storage provider must not be null.");

            TerminalId = terminalId;
            DisplayName = displayName;
            Connection = connection;
            IsLoyaltySupported = isLoyaltySupported;
            VendorPayload = vendorPayload;
            SessionStorageProvider = sessionStorageProvider;
            AbortTimeoutSeconds = vendorPayload?.AdditionalProperties.GetPropertyOrDefault(VendorPayloadKeys.AbortTimeoutSeconds, 30) ?? 30;
            PromptTimeoutSeconds = vendorPayload?.AdditionalProperties.GetPropertyOrDefault(VendorPayloadKeys.PromptTimeoutSeconds, 30) ?? 30;
            IsReversalSupported = vendorPayload?.AdditionalProperties.GetPropertyOrDefault(VendorPayloadKeys.IsReversalSupported, false) ?? false;
            IsRefundSupported = vendorPayload?.AdditionalProperties.GetPropertyOrDefault(VendorPayloadKeys.IsRefundSupported, false) ?? false;

            currentStatus = new TerminalStatus
            {
                State = TerminalState.Idle,
                PreviousState = TerminalState.Idle,
                Message = "Payment terminal initialized.",
                IsConnected = false,
                LastResultIsFinal = true,
                ActiveOperationType = OperationType.None,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        /// Updates terminal device information such as version or other metadata.
        /// Override in derived classes to load device-specific information after a successful connection test.
        /// </summary>
        /// <returns>A task that represents the device information update operation.</returns>
        protected virtual Task UpdateDeviceInfoAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Determines whether the specified terminal state represents a busy state.
        /// </summary>
        /// <param name="state">The terminal state to evaluate.</param>
        /// <returns><see langword="true"/> if the state is busy; otherwise, <see langword="false"/>.</returns>
        protected virtual bool IsBusyState(TerminalState state)
        {
            return state == TerminalState.AwaitingResult
                || state == TerminalState.TransactionInProgress
                || state == TerminalState.WaitingForUserAction
                || state == TerminalState.AwaitingPrompt;
        }

        /// <inheritdoc/>
        public virtual void UpdateConnection(TerminalConnectionOption item)
        {
            Connection = item;
        }

        /// <inheritdoc/>
        public virtual void UpdateVendorPayload(VendorPayload item)
        {
            VendorPayload = item;
        }

        /// <summary>
        /// Runs a connection test when <see cref="IsTerminalTestRequired"/> is set,
        /// restoring the terminal to <see cref="TerminalState.Idle"/> before any operation proceeds.
        /// </summary>
        private async Task EnsureTerminalReadyAsync(CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(EnsureTerminalReadyAsync)}: IsTerminalTestRequired={IsTerminalTestRequired}", GetType().FullName);

            if (!IsTerminalTestRequired)
                return;

            Trace.WriteLine($"{nameof(EnsureTerminalReadyAsync)}: terminal in Error state, running connection test.", GetType().FullName);
            await TestConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the vendor-specific abort command to the terminal.
        /// <para>
        /// Implementations MUST post status updates for all non-exception outcomes using the standard pattern:
        /// <code>
        /// var status = new TerminalStatus { ... };
        /// if (IsPaymentSessionActive)
        ///     AddSessionStatus(status);
        /// else
        ///     UpdateCurrentStatus(status);
        /// </code>
        /// Use <see cref="IsPaymentSessionActive"/> to determine whether to call <see cref="AddSessionStatus"/> 
        /// (for active sessions) or <see cref="UpdateCurrentStatus"/> (for non-session operations like loyalty abort).
        /// </para>
        /// <para>
        /// <b>For rejection:</b> Post non-final status with <see cref="FaultInfo"/> (e.g., <c>AbortRejected</c>).
        /// Session remains open and caller may retry.
        /// </para>
        /// <para>
        /// <b>For success:</b> Post final status with <see cref="TerminalState.Aborted"/>.
        /// </para>
        /// <para>
        /// <b>On communication/hardware failure:</b> Throw an exception — the base class will post 
        /// error status using the same session-aware pattern and propagate the exception.
        /// </para>
        /// <para>
        /// <see cref="AbortTransactionRequest.Force"/> is forwarded as-is from the caller.
        /// Implementations may use it to perform a best-effort abort and close the session
        /// unconditionally; it is ignored if the implementation does not support forced abort.
        /// </para>
        /// </summary>
        /// <param name="request">The abort request.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        protected abstract Task DoAbortTransactionAsync(AbortTransactionRequest request, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> AbortTransactionAsync(AbortTransactionRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(AbortTransactionAsync)}:{request}", GetType().FullName);

            EnsureCanStartOperation(OperationType.Abort);

            var currentStatus = CurrentStatus;
            bool isSessionActive = IsPaymentSessionActive;

            // Abort timeout — only applies to transaction session retries.
            if (isSessionActive && abortStartedAt.HasValue)
            {
                double elapsedSeconds = (DateTimeOffset.UtcNow - abortStartedAt.Value).TotalSeconds;

                if (elapsedSeconds >= (AbortTimeoutSeconds))
                {
                    abortStartedAt = null;

                    AddSessionStatus(new TerminalStatus
                    {
                        State = TerminalState.Idle,
                        IsConnected = currentStatus.IsConnected,
                        LastResultIsFinal = true,
                        ActiveOperationId = currentStatus.ActiveOperationId,
                        ActiveOperationType = OperationType.None,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        Message = "Abort timeout.",
                        Fault = new FaultInfo
                        {
                            Code = "AbortTimeout",
                            Message = "Transaction outcome unknown — requires manual reconciliation.",
                        },
                    });

                    return new OperationAccepted
                    {
                        OperationId = Guid.NewGuid().ToString(),
                        Message = "Abort timeout. Session closed with unknown outcome.",
                    };
                }
            }

            // Start abort retry timer for active transaction sessions.
            if (isSessionActive && abortStartedAt == null)
                abortStartedAt = DateTimeOffset.UtcNow;

            try
            {
                await DoAbortTransactionAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                abortStartedAt = null;

                var errorStatus = new TerminalStatus
                {
                    State = TerminalState.Error,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Abort failed: {ex.Message}",
                    Fault = new FaultInfo { Code = "AbortFailed", Message = ex.Message },
                };

                if (isSessionActive)
                    AddSessionStatus(errorStatus);
                else
                    UpdateCurrentStatus(errorStatus);

                Trace.WriteLine($"{nameof(AbortTransactionAsync)}:\n{ex}", GetType().FullName);
                throw;
            }

            // For non-session aborts (e.g. loyalty), post the final status here since
            // DoAbortTransactionAsync completes synchronously with no async result event.
            // For session-based aborts, the final status arrives via AddSessionStatus
            // through the AbortTransactionResultReceived event handler.
            if (!isSessionActive)
            {
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Aborted,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Abort completed.",
                });
            }

            return new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = "Abort accepted.",
            };
        }

        /// <summary>
        /// Sends the loyalty activation command to the terminal and returns once the command is accepted.
        /// Must not block until the loyalty result arrives — the result is delivered asynchronously
        /// and must be reported back via <see cref="UpdateCurrentStatus"/>.
        /// Throw if the command cannot be sent or is rejected.
        /// </summary>
        /// <param name="request">The loyalty activation request.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        protected abstract Task DoStartLoyaltyActivateAsync(LoyaltyActivateRequest request, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> LoyaltyActivateAsync(LoyaltyActivateRequest request = null, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(LoyaltyActivateAsync)}:{request}", GetType().FullName);

            await EnsureTerminalReadyAsync(cancellationToken).ConfigureAwait(false);
            EnsureCanStartOperation(OperationType.LoyaltyActivate);

            bool isConnected = CurrentStatus.IsConnected;
            string operationId = Guid.NewGuid().ToString();
            string message = "Loyalty activation accepted.";

            try
            {
                await DoStartLoyaltyActivateAsync(request, cancellationToken).ConfigureAwait(false);
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.AwaitingResult,
                    IsConnected = isConnected,
                    LastResultIsFinal = false,
                    ActiveOperationId = operationId,
                    ActiveOperationType = OperationType.LoyaltyActivate,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = message,
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(LoyaltyActivateAsync)}:\n{ex}", GetType().FullName);
                message = $"Loyalty activation failed: {ex.Message}";

                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = isConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = operationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = message,
                    Fault = new FaultInfo { Code = "LoyaltyActivateFailed", Message = ex.Message },
                });

                throw;
            }

            return new OperationAccepted
            {
                OperationId = operationId,
                Message = message,
            };
        }

        /// <summary>
        /// Sends the loyalty deactivation command to the terminal and returns once the command is accepted.
        /// Throw if the command cannot be sent or is rejected.
        /// </summary>
        /// <param name="request">The loyalty deactivation request.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        protected abstract Task DoStartLoyaltyDeactivateAsync(BaseActionRequest request, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> LoyaltyDeactivateAsync(BaseActionRequest request = null, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(LoyaltyDeactivateAsync)}:{request}", GetType().FullName);

            await EnsureTerminalReadyAsync(cancellationToken).ConfigureAwait(false);
            EnsureCanStartOperation(OperationType.LoyaltyDeactivate);

            bool isConnected = CurrentStatus.IsConnected;
            string operationId = Guid.NewGuid().ToString();
            string message = "Loyalty deactivation accepted.";

            try
            {
                await DoStartLoyaltyDeactivateAsync(request, cancellationToken).ConfigureAwait(false);
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = isConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = operationId,
                    ActiveOperationType = OperationType.LoyaltyDeactivate,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = message,
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(LoyaltyDeactivateAsync)}:\n{ex}", GetType().FullName);
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = isConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = operationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Loyalty deactivation failed: {ex.Message}",
                    Fault = new FaultInfo { Code = "LoyaltyDeactivateFailed", Message = ex.Message },
                });

                throw;
            }

            return new OperationAccepted
            {
                OperationId = operationId,
                Message = message,
            };
        }

        /// <inheritdoc/>
        public abstract Task<TerminalSettings> GetTerminalSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
		/// Retrieves the current terminal status when no session is active.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The current terminal status.</returns>
        protected abstract Task<TerminalStatus> DoGetTerminalStatusAsync(CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<TerminalStatus> GetTerminalStatusAsync(CancellationToken cancellationToken = default)
        {
            TerminalStatus status;

            lock (sessionStatusesLock)
            {
                if (isPaymentSessionActive)
                    return sessionStatuses.First.Value;

                status = currentStatus;
                if (status.ActiveOperationType == OperationType.LoyaltyActivate
                    || status.ActiveOperationType == OperationType.LoyaltyDeactivate
                    || (lastFinalStatusAt.HasValue && (DateTimeOffset.UtcNow - lastFinalStatusAt.Value).TotalSeconds < 3))
                    return status;
            }

            return await DoGetTerminalStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<TerminalSessionResponse> GetTerminalSessionAsync(CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(GetTerminalSessionAsync)}", GetType().FullName);

            lock (sessionStatusesLock)
            {
                var statuses = GetSessionStatuses();

                string sessionName = "No session";

                if (statuses.Length > 0)
                {
                    // Oldest status is last in the newest-first array — it holds the operation
                    // id that started the session.
                    var oldest = statuses[statuses.Length - 1];
                    sessionName = oldest.ActiveOperationId ?? oldest.UpdatedAt.ToString("yyyyMMddHHmmss");
                }

                return Task.FromResult(new TerminalSessionResponse
                {
                    SessionName = sessionName,
                    Statuses = statuses,
                });
            }
        }

        /// <inheritdoc/>
        public Task<OperationAccepted> ConfirmTransactionAsync(TransactionConfirmRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(ConfirmTransactionAsync)}:{request}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string sessionName;
            TerminalStatus status;
            string operationId = Guid.NewGuid().ToString();

            lock (sessionStatusesLock)
            {
                if (isPaymentSessionActive)
                    throw new ApiConflictException("Cannot confirm a transaction while a session is active. Wait for the session to complete first.");

                status = currentStatus;

                if (!status.LastResultIsFinal)
                    throw new ApiConflictException("Cannot confirm: no final transaction result is available.");

                // Idempotent: already confirmed by a concurrent or previous call.
                if (status.State == TerminalState.Idle
                    && status.ActiveOperationType == OperationType.None
                    && string.Equals(status.Message, "Transaction confirmed.", StringComparison.Ordinal))
                {
                    return Task.FromResult(new OperationAccepted
                    {
                        OperationId = status.ActiveOperationId ?? operationId,
                        Message = "Transaction confirmed.",
                    });
                }

                sessionName = status.SessionName
                    ?? request.VendorPayload?.AdditionalProperties.GetPropertyOrDefault<string>(VendorPayloadKeys.SessionName);

                // Atomically advance currentStatus to confirmed inside the lock so a concurrent
                // request racing through the idempotency check above sees the confirmed state
                // before either caller reaches ConfirmSession.
                previousStatus = currentStatus;
                currentStatus = new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = status.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = operationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Transaction confirmed.",
                };
            }

            if (!string.IsNullOrWhiteSpace(sessionName))
            {
                lock (sessionPersistenceLock)
                {
                    SessionStorageProvider.ConfirmSession(sessionName);
                }
            }

            Trace.WriteLine($"{nameof(ConfirmTransactionAsync)}: {status} => confirmed", GetType().FullName);

            return Task.FromResult(new OperationAccepted
            {
                OperationId = operationId,
                Message = "Transaction confirmed.",
            });
        }

        /// <summary>
        /// Performs vendor-specific release of terminal resources.
        /// Called by <see cref="ReleaseAsync"/> after abort completes or times out.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        protected abstract Task<OperationAccepted> DoReleaseAsync(CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> ReleaseAsync()
        {
            Trace.WriteLine($"{nameof(ReleaseAsync)}", GetType().FullName);

            if (IsPaymentSessionActive)
            {
                try
                {
                    await AbortTransactionAsync(new AbortTransactionRequest { Force = true }, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{nameof(ReleaseAsync)}:\n{ex}", GetType().FullName);
                }
            }

            return await DoReleaseAsync(CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the prompt response to the terminal.
        /// </summary>
        /// <param name="request">The prompt response request.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        protected abstract Task<OperationAccepted> DoRespondToPromptAsync(PromptResponseRequest request, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> RespondToPromptAsync(PromptResponseRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(RespondToPromptAsync)}:{request}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var status = CurrentStatus;

            if (status.State != TerminalState.AwaitingPrompt)
                throw new ApiConflictException($"Cannot respond to a prompt when terminal state is {status.State}. Expected {TerminalState.AwaitingPrompt}.");

            if (status.Prompt == null)
                throw new ApiConflictException("Cannot respond to a prompt when no active prompt is present.");

            if (!string.Equals(status.Prompt.PromptId, request.PromptId, StringComparison.Ordinal))
                throw new ApiConflictException($"Prompt ID mismatch. Active prompt is '{status.Prompt.PromptId}', responded to '{request.PromptId}'.");

            return await DoRespondToPromptAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the purchase command to the terminal and returns once accepted.
        /// </summary>
        /// <param name="request">The purchase request.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        protected abstract Task<OperationAccepted> DoStartPurchaseAsync(PurchaseRequest request, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> StartPurchaseAsync(PurchaseRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(StartPurchaseAsync)}:{request}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            await EnsureTerminalReadyAsync(cancellationToken).ConfigureAwait(false);
            EnsureCanStartOperation(OperationType.Purchase);

            var currentStatus = CurrentStatus;
            bool isConnected = currentStatus.IsConnected;
            string operationId = Guid.NewGuid().ToString();
            string clientReference = request.ClientReference;

            // If loyalty was active, carry forward the loyalty status as the first session entry.
            var initialStatus = new TerminalStatus
            {
                State = TerminalState.TransactionInProgress,
                IsConnected = isConnected,
                LastResultIsFinal = false,
                ActiveOperationId = operationId,
                ActiveOperationType = OperationType.Purchase,
                ClientReference = clientReference,
                UpdatedAt = DateTimeOffset.UtcNow,
                Message = "Purchase started.",
            };

            StartSession(initialStatus);

            try
            {
                await DoStartPurchaseAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AddSessionStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = isConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = operationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Purchase failed to start: {ex.Message}",
                    Fault = new FaultInfo { Code = "PurchaseStartFailed", Message = ex.Message },
                });
                Trace.WriteLine($"{nameof(StartPurchaseAsync)}:\n{ex}", GetType().FullName);
                throw;
            }

            return new OperationAccepted
            {
                OperationId = operationId,
                Message = "Purchase accepted.",
            };
        }

        /// <summary>
        /// Sends the refund command to the terminal and returns once accepted.
        /// </summary>
        /// <param name="request">The refund request.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        protected abstract Task<OperationAccepted> DoStartRefundAsync(RefundRequest request, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> StartRefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(StartRefundAsync)}:{request}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            await EnsureTerminalReadyAsync(cancellationToken).ConfigureAwait(false);
            EnsureCanStartOperation(OperationType.Refund);

            bool isConnected = CurrentStatus.IsConnected;
            string operationId = Guid.NewGuid().ToString();

            StartSession(new TerminalStatus
            {
                State = TerminalState.TransactionInProgress,
                IsConnected = isConnected,
                LastResultIsFinal = false,
                ActiveOperationId = operationId,
                ActiveOperationType = OperationType.Refund,
                ClientReference = request.ClientReference,
                UpdatedAt = DateTimeOffset.UtcNow,
                Message = "Refund started.",
            });

            try
            {
                await DoStartRefundAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AddSessionStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = isConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = operationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Refund failed to start: {ex.Message}",
                    Fault = new FaultInfo { Code = "RefundStartFailed", Message = ex.Message },
                });
                Trace.WriteLine($"{nameof(StartRefundAsync)}:\n{ex}", GetType().FullName);
                throw;
            }

            return new OperationAccepted
            {
                OperationId = operationId,
                Message = "Refund accepted.",
            };
        }

        /// <summary>
        /// Sends the reversal command to the terminal and returns once accepted.
        /// </summary>
        /// <param name="request">The reversal request.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        protected abstract Task<OperationAccepted> DoStartReversalAsync(ReversalRequest request, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> StartReversalAsync(ReversalRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(StartReversalAsync)}:{request}", GetType().FullName);

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            await EnsureTerminalReadyAsync(cancellationToken).ConfigureAwait(false);
            EnsureCanStartOperation(OperationType.Reversal);

            bool isConnected = CurrentStatus.IsConnected;
            string operationId = Guid.NewGuid().ToString();

            StartSession(new TerminalStatus
            {
                State = TerminalState.TransactionInProgress,
                IsConnected = isConnected,
                LastResultIsFinal = false,
                ActiveOperationId = operationId,
                ActiveOperationType = OperationType.Reversal,
                ClientReference = request.ClientReference,
                UpdatedAt = DateTimeOffset.UtcNow,
                Message = "Reversal started.",
            });

            try
            {
                await DoStartReversalAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AddSessionStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = isConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = operationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Reversal failed to start: {ex.Message}",
                    Fault = new FaultInfo { Code = "ReversalStartFailed", Message = ex.Message },
                });
                Trace.WriteLine($"{nameof(StartReversalAsync)}:\n{ex}", GetType().FullName);
                throw;
            }

            return new OperationAccepted
            {
                OperationId = operationId,
                Message = "Reversal accepted.",
            };
        }

        /// <summary>
        /// Performs the vendor-specific connection test.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>The accepted operation response.</returns>
        protected abstract Task<OperationAccepted> DoTestConnectionAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public async Task<OperationAccepted> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(TestConnectionAsync)}", GetType().FullName);

            if (IsPaymentSessionActive)
                throw new ApiConflictException("Cannot run a connection test while a session is active.");

            OperationAccepted result = null;
            bool isConnected = false;
            string message = null;
            string faultCode = null;
            ExceptionDispatchInfo capturedError = null;

            try
            {
                result = await DoTestConnectionAsync(cancellationToken).ConfigureAwait(false);
                isConnected = true;
                message = result?.Message ?? "Connection test succeeded.";
            }
            catch (Exception ex)
            {
                isConnected = false;
                message = $"Connection test failed: {ex.Message}";
                faultCode = "ConnectionTestFailed";
                capturedError = ExceptionDispatchInfo.Capture(ex);
                Trace.WriteLine($"{nameof(TestConnectionAsync)}:\n{ex}", GetType().FullName);
            }

            if (capturedError == null && isConnected && !IsDeviceInfoUpdated)
            {
                try
                {
                    await UpdateDeviceInfoAsync().ConfigureAwait(false);
                    IsDeviceInfoUpdated = true;
                    message = "Connection test and device info update succeeded.";
                }
                catch (Exception ex)
                {
                    message = $"Connection test succeeded, but device info update failed: {ex.Message}";
                    faultCode = "DeviceInfoUpdateFailed";
                    capturedError = ExceptionDispatchInfo.Capture(ex);
                    Trace.WriteLine($"{nameof(TestConnectionAsync)}:\n{ex}", GetType().FullName);
                }
            }

            var status = new TerminalStatus
            {
                State = TerminalState.Idle,
                IsConnected = isConnected,
                LastResultIsFinal = true,
                ActiveOperationId = result?.OperationId,
                ActiveOperationType = OperationType.None,
                UpdatedAt = DateTimeOffset.UtcNow,
                Message = message,
            };

            if (capturedError != null)
            {
                status.Fault = new FaultInfo
                {
                    Code = faultCode,
                    Message = message,
                };
            }

            UpdateCurrentStatus(status);

            if (capturedError != null)
                capturedError.Throw();

            return result;
        }

        /// <summary>
        /// Determines whether the specified exception represents a physical disconnection from the terminal.
        /// </summary>
        protected static bool IsDisconnectionError(Exception exception)
        {
            if (exception == null)
                return false;

            return exception is System.IO.IOException
                || exception is TimeoutException
                || (exception is InvalidOperationException && exception.Message.IndexOf("port", StringComparison.OrdinalIgnoreCase) >= 0)
                || IsDisconnectionError(exception.InnerException);
        }

        /// <summary>
        /// Handles a terminal error by updating status through the appropriate path.
        /// When a session is active the error is recorded as a final fault status in the session.
        /// When no session is active <see cref="UpdateCurrentStatus"/> is called directly.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is <c>null</c>.</exception>
        protected void HandleTerminalError(Exception exception)
        {
            Trace.WriteLine($"{nameof(HandleTerminalError)}: {exception}", GetType().FullName);

            bool isDisconnected = IsDisconnectionError(exception);
            string faultCode = isDisconnected ? "TerminalDisconnected" : "TerminalError";
            string message = $"Terminal error: {exception.Message}";

            var currentStatus = CurrentStatus;
            bool isSessionActive = IsPaymentSessionActive;
            bool isConnected = !isDisconnected && currentStatus.IsConnected;

            var terminalStatus = new TerminalStatus
            {
                State = TerminalState.Error,
                IsConnected = isConnected,
                LastResultIsFinal = true,
                ActiveOperationId = isSessionActive ? currentStatus.ActiveOperationId : null,
                ActiveOperationType = OperationType.None,
                UpdatedAt = DateTimeOffset.UtcNow,
                Message = message,
                Fault = new FaultInfo { Code = faultCode, Message = exception.Message },
            };

            if (isSessionActive)
                AddSessionStatus(terminalStatus);
            else
                UpdateCurrentStatus(terminalStatus);
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            if (!disposed)
            {
                try
                {
                    ReleaseAsync().GetAwaiter().GetResult();  // Block until terminal is released
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{nameof(Dispose)}:\n{ex}", GetType().FullName);
                }

                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Disposes resources. Override to add custom cleanup logic.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
            }
        }
    }
}