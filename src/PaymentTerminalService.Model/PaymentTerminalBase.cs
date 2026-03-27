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

        /// <summary>
        /// VendorPayload property name for the abort timeout in seconds.
        /// </summary>
        public const string AbortTimeoutSecondsPropertyName = "abortTimeoutSeconds";

        /// <summary>
        /// VendorPayload property name for the prompt response timeout in seconds.
        /// </summary>
        public const string PromptTimeoutSecondsPropertyName = "promptTimeoutSeconds";

        /// <summary>
        /// VendorPayload property name for the session name.
        /// </summary>
        public const string SessionNamePropertyName = "sessionName";

        private bool disposed;

        private DateTimeOffset? abortStartedAt;

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

            AbortTimeoutSeconds = vendorPayload?.AdditionalProperties
                .GetPropertyOrDefault(AbortTimeoutSecondsPropertyName, 30)
                ?? 30;

            PromptTimeoutSeconds = vendorPayload?.AdditionalProperties
                .GetPropertyOrDefault(PromptTimeoutSecondsPropertyName, 30)
                ?? 30;

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
        /// Sends the vendor-specific abort command to the hardware.
        /// For active transaction sessions: sends the abort command — the result arrives
        /// asynchronously and must be reported back via <see cref="AddSessionStatus"/>.
        /// For active loyalty sessions: deactivates loyalty and closes the session directly.
        /// Throw if the command cannot be sent.
        /// </summary>
        protected abstract Task DoAbortAsync(CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> AbortTransactionAsync(CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(AbortTransactionAsync)}", GetType().FullName);

            EnsureCanStartOperation(OperationType.Abort);

            var status = CurrentStatus;
            bool isConnected = status?.IsConnected ?? false;
            bool isSessionActive = IsPaymentSessionActive;
            bool isLoyaltyActive = !isSessionActive
                && status?.ActiveOperationType == OperationType.LoyaltyActivate;

            // Abort timeout — only applies to transaction session retries.
            if (isSessionActive && abortStartedAt.HasValue)
            {
                double elapsedSeconds = (DateTimeOffset.UtcNow - abortStartedAt.Value).TotalSeconds;

                if (elapsedSeconds >= AbortTimeoutSeconds)
                {
                    abortStartedAt = null;

                    AddSessionStatus(new TerminalStatus
                    {
                        State = TerminalState.Idle,
                        IsConnected = isConnected,
                        LastResultIsFinal = true,
                        ActiveOperationId = status.ActiveOperationId,
                        ActiveOperationType = OperationType.None,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        Message = "Abort timeout elapsed. Transaction outcome unknown — requires manual reconciliation.",
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

            // Post interim status only for active transaction sessions.
            if (isSessionActive)
            {
                if (abortStartedAt == null)
                    abortStartedAt = DateTimeOffset.UtcNow;

                AddSessionStatus(new TerminalStatus
                {
                    State = status.State,
                    IsConnected = isConnected,
                    LastResultIsFinal = false,
                    ActiveOperationId = status.ActiveOperationId,
                    ActiveOperationType = OperationType.Abort,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Abort requested.",
                });
            }

            try
            {
                await DoAbortAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                abortStartedAt = null;

                if (isSessionActive)
                {
                    AddSessionStatus(new TerminalStatus
                    {
                        State = TerminalState.Idle,
                        IsConnected = isConnected,
                        LastResultIsFinal = true,
                        ActiveOperationId = status.ActiveOperationId,
                        ActiveOperationType = OperationType.None,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        Message = $"Abort failed: {ex.Message}",
                        Fault = new FaultInfo { Code = "AbortFailed", Message = ex.Message },
                    });
                }

                throw;
            }

            // Non-session loyalty abort — DoAbortAsync deactivated loyalty and returned.
            // Reset to Idle now. No interim status was posted before DoAbortAsync —
            // state must not change until the command succeeds.
            if (isLoyaltyActive)
            {
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = isConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = status.ActiveOperationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Loyalty deactivated by abort.",
                });
            }

            return new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = isLoyaltyActive ? "Loyalty deactivated." : "Abort accepted.",
            };
        }

        /// <summary>
        /// Sends the loyalty activation command to the hardware and returns once the command is accepted.
        /// Must not block until the loyalty result arrives — the result is delivered asynchronously
        /// and must be reported back via <see cref="UpdateCurrentStatus"/>.
        /// Throw if the command cannot be sent or is rejected.
        /// </summary>
        protected abstract Task DoStartLoyaltyActivateAsync(LoyaltyActivateRequest request, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> LoyaltyActivateAsync(LoyaltyActivateRequest request = null, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(LoyaltyActivateAsync)}:{request}", GetType().FullName);

            await EnsureTerminalReadyAsync(cancellationToken).ConfigureAwait(false);
            EnsureCanStartOperation(OperationType.LoyaltyActivate);

            bool isConnected = CurrentStatus.IsConnected;
            string operationId = Guid.NewGuid().ToString();

            UpdateCurrentStatus(new TerminalStatus
            {
                State = TerminalState.AwaitingResult,
                IsConnected = isConnected,
                LastResultIsFinal = false,
                ActiveOperationId = operationId,
                ActiveOperationType = OperationType.LoyaltyActivate,
                UpdatedAt = DateTimeOffset.UtcNow,
                Message = "Loyalty activation in progress.",
            });

            try
            {
                await DoStartLoyaltyActivateAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = isConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = operationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Loyalty activation failed: {ex.Message}",
                    Fault = new FaultInfo { Code = "LoyaltyActivateFailed", Message = ex.Message },
                });
                throw;
            }

            return new OperationAccepted
            {
                OperationId = operationId,
                Message = "Loyalty activation accepted.",
            };
        }

        /// <summary>
        /// Sends the loyalty deactivation command to the hardware and returns once the command is accepted.
        /// Throw if the command cannot be sent or is rejected.
        /// </summary>
        protected abstract Task DoStartLoyaltyDeactivateAsync(BaseActionRequest request, CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> LoyaltyDeactivateAsync(BaseActionRequest request = null, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(LoyaltyDeactivateAsync)}:{request}", GetType().FullName);

            await EnsureTerminalReadyAsync(cancellationToken).ConfigureAwait(false);
            EnsureCanStartOperation(OperationType.LoyaltyDeactivate);

            bool isConnected = CurrentStatus.IsConnected;
            string operationId = Guid.NewGuid().ToString();

            try
            {
                await DoStartLoyaltyDeactivateAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
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

            UpdateCurrentStatus(new TerminalStatus
            {
                State = TerminalState.Idle,
                IsConnected = isConnected,
                LastResultIsFinal = true,
                ActiveOperationId = operationId,
                ActiveOperationType = OperationType.None,
                UpdatedAt = DateTimeOffset.UtcNow,
                Message = "Loyalty deactivated.",
            });

            return new OperationAccepted
            {
                OperationId = operationId,
                Message = "Loyalty deactivated.",
            };
        }

        /// <inheritdoc/>
        public abstract Task<TerminalSettings> GetTerminalSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>
		/// Retrieves the current terminal status from the underlying hardware when no session is active.
        /// </summary>
        protected abstract Task<TerminalStatus> DoGetTerminalStatusAsync(CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<TerminalStatus> GetTerminalStatusAsync(CancellationToken cancellationToken = default)
        {
            if (IsPaymentSessionActive)
                return sessionStatuses.First.Value;

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
                    ?? request.VendorPayload?.AdditionalProperties
                        .GetPropertyOrDefault<string>(SessionNamePropertyName);

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
        /// Performs vendor-specific release of hardware resources.
        /// Called by <see cref="ReleaseAsync"/> after abort completes or times out.
        /// </summary>
        protected abstract Task<OperationAccepted> DoReleaseAsync(CancellationToken cancellationToken);

        /// <inheritdoc/>
        public async Task<OperationAccepted> ReleaseAsync()
        {
            Trace.WriteLine($"{nameof(ReleaseAsync)}", GetType().FullName);

            if (IsPaymentSessionActive)
            {
                try
                {
                    await AbortTransactionAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{nameof(ReleaseAsync)}: abort before release failed: {ex}", GetType().FullName);
                }
            }

            return await DoReleaseAsync(CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends the prompt response to the hardware.
        /// </summary>
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
        /// Sends the purchase command to the hardware and returns once accepted.
        /// </summary>
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
                throw;
            }

            return new OperationAccepted
            {
                OperationId = operationId,
                Message = "Purchase accepted.",
            };
        }

        /// <summary>
        /// Sends the refund command to the hardware and returns once accepted.
        /// </summary>
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
                throw;
            }

            return new OperationAccepted
            {
                OperationId = operationId,
                Message = "Refund accepted.",
            };
        }

        /// <summary>
        /// Sends the reversal command to the hardware and returns once accepted.
        /// </summary>
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
            Dispose(true);
            GC.SuppressFinalize(this);
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