using PaymentTerminalService.Model;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentTerminalService.Terminals
{
    /// <summary>
    /// Represents a mock implementation of a payment terminal for development and testing.
    /// Simulates terminal operations, state transitions, and responses without requiring real hardware.
    /// This class is intended for use in DEBUG mode only.
    /// </summary>
    internal partial class TestPaymentTerminal : PaymentTerminalBase
    {
        public const string VendorString = "TestVendor";
        public const string ModelString = "Model X";

        // Active prompt blocking the simulation thread, awaiting POS response via REST.
        private volatile PendingPrompt pendingPrompt;

        /// <inheritdoc/>
        public override string Vendor => VendorString;

        /// <inheritdoc/>
        public override string Model => ModelString;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestPaymentTerminal"/> class.
        /// </summary>
        /// <param name="terminalId">Unique identifier for the terminal.</param>
        /// <param name="displayName">Display name for the terminal.</param>
        /// <param name="connection">Connection options (not used at runtime by this mock).</param>
        /// <param name="isLoyaltySupported">Indicates whether loyalty operations are simulated.</param>
        /// <param name="vendorPayload">Vendor-specific configuration payload.</param>
        /// <param name="sessionStorageProvider">Optional session storage provider for persisting simulated session data.</param>
        public TestPaymentTerminal(
            string terminalId,
            string displayName,
            TerminalConnectionOption connection,
            bool isLoyaltySupported,
            VendorPayload vendorPayload,
            ISessionStorageProvider sessionStorageProvider = null)
            : base(
                terminalId,
                displayName,
                connection,
                isLoyaltySupported,
                vendorPayload,
                sessionStorageProvider)
        {
            StartOrphanSimulation();
        }

        protected override Task UpdateDeviceInfoAsync()
        {
            Trace.WriteLine($"{nameof(UpdateDeviceInfoAsync)}", GetType().FullName);
            Version = "Test version 1";
            return Task.CompletedTask;
        }

        protected override Task<TerminalStatus> DoGetTerminalStatusAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(CurrentStatus);
        }

        /// <inheritdoc/>
        protected override Task DoAbortTransactionAsync(AbortTransactionRequest request, CancellationToken cancellationToken)
        {
            var currentStatus = CurrentStatus;
            bool isSessionActive = IsPaymentSessionActive;

            int roll = random.Next(0, 100);

            // Simulate communication/hardware failure
            if (roll < AbortExceptionThreshold)
            {
                throw new InvalidOperationException("Simulated abort communication failure.");
            }

            if (request.Force)
                return Task.CompletedTask;

            // Simulate persistent rejection (to test timeout scenario)
            bool isPersistentRejection = roll < (AbortExceptionThreshold + AbortPersistentRejectionThreshold);
            
            // Regular rejection (random per call)
            bool isRejected = isPersistentRejection 
                || roll < (AbortExceptionThreshold + AbortPersistentRejectionThreshold + AbortRejectedThreshold);

            TerminalStatus status;
            string message = "Abort operation completed.";

            if (isRejected)
            {
                message = "Abort operation rejected.";
                // Terminal rejected abort — session stays open, caller may retry.
                status = new TerminalStatus
                {
                    State = currentStatus.State,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = false,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = currentStatus.ActiveOperationType,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = message,
                    Fault = new FaultInfo { Code = "AbortRejected", Message = message },
                };
            }
            else
            {
                // Terminal accepted abort — session closes.
                status = new TerminalStatus
                {
                    State = TerminalState.Aborted,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = message,
                };
            }

            // Post status using session-aware pattern
            if (isSessionActive)
                AddSessionStatus(status);
            else
                UpdateCurrentStatus(status);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override Task DoStartLoyaltyActivateAsync(LoyaltyActivateRequest request, CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoStartLoyaltyActivateAsync)}:{request}", GetType().FullName);

            var currentStatus = CurrentStatus;
            string operationId = currentStatus?.ActiveOperationId;
            bool isConnected = currentStatus?.IsConnected ?? false;
            int roll = random.Next(0, 100);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(1500);

                try
                {
                    if (!IsLoyaltyOperationActive(operationId))
                        return;

                    // 20 % failure — bonus card not recognized.
                    if (roll < BonusReadFailedThreshold)
                    {
                        UpdateCurrentStatus(new TerminalStatus
                        {
                            State = TerminalState.Idle,
                            IsConnected = isConnected,
                            LastResultIsFinal = true,
                            ActiveOperationId = operationId,
                            ActiveOperationType = OperationType.None,
                            UpdatedAt = DateTimeOffset.UtcNow,
                            Message = "Bonus card read failed: card not recognized.",
                            Fault = new FaultInfo { Code = "1002", Message = "Card read failed." },
                        });
                        return;
                    }

                    string customerNumber = $"TEST{random.Next(10000, 99999)}";
                    string memberClass = random.Next(0, 2) == 0 ? "Gold" : "Silver";

                    bool isSessionActive = IsPaymentSessionActive;

                    if (isSessionActive)
                    {
                        // Bonus read completed during an active transaction — session owns the state.
                        var sessionStatus = CurrentStatus;
                        AddSessionStatus(new TerminalStatus
                        {
                            State = sessionStatus.State,
                            IsConnected = sessionStatus.IsConnected,
                            LastResultIsFinal = false,
                            ActiveOperationId = sessionStatus.ActiveOperationId,
                            ActiveOperationType = sessionStatus.ActiveOperationType,
                            ClientReference = sessionStatus.ClientReference,
                            UpdatedAt = DateTimeOffset.UtcNow,
                            Message = $"Bonus card read. Customer={customerNumber} MemberClass={memberClass}.",
                        });
                    }
                    else
                    {
                        // Loyalty standalone — bonus read complete, terminal returns to Idle.
                        UpdateCurrentStatus(new TerminalStatus
                        {
                            State = TerminalState.Idle,
                            IsConnected = isConnected,
                            LastResultIsFinal = true,
                            ActiveOperationId = operationId,
                            ActiveOperationType = OperationType.None,
                            UpdatedAt = DateTimeOffset.UtcNow,
                            Message = $"Bonus card read. Customer={customerNumber} MemberClass={memberClass}.",
                        });
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{nameof(DoStartLoyaltyActivateAsync)} simulated bonus update failed: {ex}", GetType().FullName);
                }
            });

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override Task DoStartLoyaltyDeactivateAsync(BaseActionRequest request, CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoStartLoyaltyDeactivateAsync)}:{request}", GetType().FullName);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override Task<OperationAccepted> DoStartPurchaseAsync(PurchaseRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(DoStartPurchaseAsync)}:{request}", GetType().FullName);

            bool withBonus = IsLoyaltySupported && random.Next(0, 2) == 0;
            SimulatePurchaseAsync(request.Amount, request.Currency ?? "EUR", withBonus);

            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = "Purchase started.",
            });
        }

        /// <inheritdoc/>
        protected override Task<OperationAccepted> DoStartRefundAsync(RefundRequest request, CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoStartRefundAsync)}:{request}", GetType().FullName);

            SimulateSimpleTransactionAsync("Refund", "Refund", request?.Amount ?? 0, "EUR");

            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = "Refund started.",
            });
        }

        /// <inheritdoc/>
        protected override Task<OperationAccepted> DoStartReversalAsync(ReversalRequest request, CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoStartReversalAsync)}:{request}", GetType().FullName);

            long amount = 0;
            string currency = "EUR";

            var priorPayload = CurrentStatus?.OperationResult?.VendorPayload?.AdditionalProperties;
            if (priorPayload != null)
            {
                object rawAmount;
                if (priorPayload.TryGetValue("amount", out rawAmount) && rawAmount is long longAmount)
                    amount = longAmount;

                object rawCurrency;
                if (priorPayload.TryGetValue("currency", out rawCurrency) && rawCurrency is string stringCurrency
                    && !string.IsNullOrEmpty(stringCurrency))
                    currency = stringCurrency;
            }

            SimulateSimpleTransactionAsync("Reversal", "Reversal", amount, currency);

            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = "Reversal started.",
            });
        }

        /// <inheritdoc/>
        public override Task<TerminalSettings> GetTerminalSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TerminalSettings
            {
                VendorPayload = VendorPayload ?? new VendorPayload(),
                AdditionalProperties = new System.Collections.Generic.Dictionary<string, object>
                {
                    { nameof(IsLoyaltySupported), IsLoyaltySupported }
                }
            });
        }

        /// <inheritdoc/>
        protected override Task<OperationAccepted> DoReleaseAsync(CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoReleaseAsync)}", GetType().FullName);

            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = "Terminal released.",
            });
        }

        /// <inheritdoc/>
        protected override Task<OperationAccepted> DoRespondToPromptAsync(PromptResponseRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(DoRespondToPromptAsync)}: {request?.PromptId}", GetType().FullName);

            var pending = pendingPrompt;

            if (pending == null)
                throw new ApiConflictException("No active prompt to respond to.");

            if (!string.Equals(pending.PromptId, request.PromptId, StringComparison.Ordinal))
                throw new ApiConflictException($"Prompt ID mismatch. Active prompt is '{pending.PromptId}', responded to '{request.PromptId}'.");

            pending.Reply(request.YesNo, request.Input);

            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = "Prompt response accepted.",
            });
        }

        /// <inheritdoc/>
        protected override Task<OperationAccepted> DoTestConnectionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = "Terminal connection test succeeded.",
            });
        }

        protected override void Dispose(bool disposing)
        {
            Trace.WriteLine($"{nameof(Dispose)}:{disposing}", GetType().FullName);

            if (disposing)
                StopOrphanSimulation();

            base.Dispose(disposing);
        }
    }
}
