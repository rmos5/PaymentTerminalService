using PaymentTerminalService.Model;
using System;
using System.Collections.Generic;
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
    internal class TestPaymentTerminal : PaymentTerminalBase
    {
        public const string VendorString = "TestVendor";
        public const string ModelString = "Model X";

        // Probability thresholds out of 100.
        private const int DeclinedThreshold = 15;        // 15 % transaction declined
        private const int ConnectionErrorThreshold = 5;  // 5 %  connection lost during auth
        private const int AbortRejectedThreshold = 20;   // 20 % abort rejected by terminal
        private const int BonusReadFailedThreshold = 20; // 20 % bonus card read fails

        // Active prompt blocking the simulation thread, awaiting POS response via REST.
        private volatile PendingPrompt pendingPrompt;

        private readonly Random random = new Random();

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
        protected override Task DoAbortAsync(CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoAbortAsync)}", GetType().FullName);

            var currentStatus = CurrentStatus;

            if (!IsPaymentSessionActive)
                return Task.CompletedTask;

            bool isRejected = random.Next(0, 100) < AbortRejectedThreshold;

            if (isRejected)
            {
                // Terminal rejected abort — mirrors TerminalManager_AbortTransactionResultReceived
                // with IsAborted = false. Session stays open; caller may retry.
                AddSessionStatus(new TerminalStatus
                {
                    State = currentStatus.State,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = false,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = currentStatus.ActiveOperationType,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Abort rejected by terminal: authorization in progress.",
                    Fault = new FaultInfo { Code = "AbortRejected", Message = "Abort rejected: authorization in progress." },
                });
            }
            else
            {
                // Mirrors TerminalManager_AbortTransactionResultReceived with IsAborted = true.
                AddSessionStatus(new TerminalStatus
                {
                    State = TerminalState.Aborted,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Transaction aborted.",
                });
            }

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

                    var bonusStatusPayload = new VendorPayload();
                    bonusStatusPayload.AdditionalProperties["bonusCustomerNumber"] = customerNumber;
                    bonusStatusPayload.AdditionalProperties["bonusMemberClass"] = memberClass;
                    bonusStatusPayload.AdditionalProperties["bonusStatusCode"] = string.Empty;
                    bonusStatusPayload.AdditionalProperties["bonusStatusText"] = "Bonus card accepted.";

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
            base.Dispose(disposing);
        }

        // Contactless flow: WaitingForUserAction(card tap) → TransactionInProgress for all remaining phases.
        // Each entry: phase code, message, delay ms, TerminalState.
        //   WaitingForUserAction — cardholder must do something (insert card, enter PIN)
        //   TransactionInProgress — terminal is processing, no cardholder action needed
        private static readonly object[][] SimulatedContactlessPhases = new object[][]
        {
            new object[] { "0", "Waiting for card.",          2000, TerminalState.WaitingForUserAction  },
            new object[] { "9", "Contactless card read.",      800, TerminalState.TransactionInProgress },
            new object[] { "8", "Authorization in progress.", 2500, TerminalState.TransactionInProgress },
            new object[] { "R", "Transaction complete, waiting for card removal.", 800, TerminalState.TransactionInProgress },
        };

        // Chip/PIN flow: WaitingForUserAction(insert card, PIN) → TransactionInProgress for all remaining phases.
        private static readonly object[][] SimulatedChipPinPhases = new object[][]
        {
            new object[] { "0", "Waiting for card.",                   2000, TerminalState.WaitingForUserAction  },
            new object[] { "1", "Chip card inserted.",                  500, TerminalState.WaitingForUserAction  },
            new object[] { "7", "Cardholder verification (e.g. PIN).", 3000, TerminalState.WaitingForUserAction  },
            new object[] { "A", "Transaction initialized.",             500, TerminalState.TransactionInProgress },
            new object[] { "8", "Authorization in progress.",          2500, TerminalState.TransactionInProgress },
            new object[] { "R", "Transaction complete, waiting for card removal.", 800, TerminalState.TransactionInProgress },
        };

        private static readonly object[][] SimulatedSimplePhases = new object[][]
        {
            new object[] { "8", "Authorization in progress.", 2000, TerminalState.TransactionInProgress },
            new object[] { "R", "Transaction complete, waiting for card removal.", 800, TerminalState.TransactionInProgress },
        };

        private static readonly string[][] DeclineCodes = new string[][]
        {
            new[] { "9100", "Not approved." },
            new[] { "1002", "Card read failed." },
            new[] { "1013", "Timeout during application selection or PIN." },
            new[] { "9116", "Insufficient funds." },
            new[] { "1006", "Card expired." },
        };

        /// <summary>
        /// Returns true when loyalty with <paramref name="operationId"/> is still the active operation.
        /// Guards the background loyalty thread against posting status after abort has cleared it.
        /// </summary>
        private bool IsLoyaltyOperationActive(string operationId)
        {
            var status = CurrentStatus;
            return status != null
                && status.ActiveOperationId == operationId
                && status.ActiveOperationType == OperationType.LoyaltyActivate;
        }

        private void SimulatePurchaseAsync(long amount, string currency, bool withBonus)
        {
            string operationId = CurrentStatus?.ActiveOperationId;
            string clientReference = CurrentStatus?.ClientReference;
            bool isConnected = CurrentStatus?.IsConnected ?? false;

            PurchaseScenario scenario = DequeuePurchaseScenario();

            // Randomly pick contactless or chip/PIN flow — both are equally common in practice.
            // Prompts require the chip/PIN flow; override to chip/PIN when a prompt scenario is selected.
            bool isChipPin = scenario == PurchaseScenario.PromptYesNo
                || scenario == PurchaseScenario.PromptInput
                || random.Next(0, 2) == 0;

            object[][] phases = isChipPin ? SimulatedChipPinPhases : SimulatedContactlessPhases;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    foreach (var phase in phases)
                    {
                        Thread.Sleep((int)phase[2]);

                        if (!IsPaymentSessionActive)
                            return;

                        // Connection lost during authorization.
                        if ((string)phase[0] == "8" && scenario == PurchaseScenario.ConnectionError)
                        {
                            AddSessionStatus(new TerminalStatus
                            {
                                State = TerminalState.Error,
                                IsConnected = false,
                                LastResultIsFinal = true,
                                ActiveOperationId = operationId,
                                ActiveOperationType = OperationType.None,
                                ClientReference = clientReference,
                                UpdatedAt = DateTimeOffset.UtcNow,
                                Message = "Connection lost during authorization.",
                                Fault = new FaultInfo { Code = "TerminalDisconnected", Message = "Connection lost during authorization." },
                            });
                            return;
                        }

                        AddSessionStatus(new TerminalStatus
                        {
                            State = (TerminalState)phase[3],
                            IsConnected = isConnected,
                            LastResultIsFinal = false,
                            ActiveOperationId = operationId,
                            ActiveOperationType = OperationType.Purchase,
                            ClientReference = clientReference,
                            UpdatedAt = DateTimeOffset.UtcNow,
                            Message = $"{(string)phase[1]} {amount / 100m:F2} {currency}".Trim(),
                        });

                        if ((string)phase[0] == "7")
                        {
                            // Yes/No prompt scenario — PIN bypass confirmation.
                            if (scenario == PurchaseScenario.PromptYesNo)
                            {
                                bool promptAccepted = SimulatePrompt("PIN bypass requested. Confirm?" );

                                if (!IsPaymentSessionActive)
                                    return;

                                if (!promptAccepted)
                                {
                                    AddSessionStatus(new TerminalStatus
                                    {
                                        State = TerminalState.Idle,
                                        IsConnected = isConnected,
                                        LastResultIsFinal = true,
                                        ActiveOperationId = operationId,
                                        ActiveOperationType = OperationType.None,
                                        ClientReference = clientReference,
                                        UpdatedAt = DateTimeOffset.UtcNow,
                                        Message = "Transaction aborted: PIN bypass declined or timed out.",
                                        Fault = new FaultInfo { Code = "PromptDeclined", Message = "PIN bypass declined or timed out." },
                                    });
                                    return;
                                }
                            }

                            // Input prompt scenario — manual authorization code.
                            if (scenario == PurchaseScenario.PromptInput)
                            {
                                var inputSpec = new PromptInputSpec
                                {
                                    Kind = PromptInputSpecKind.Digits,
                                    MinLength = 4,
                                    MaxLength = 6,
                                    Hint = "Enter authorization code (4-6 digits).",
                                };

                                string authCode;
                                bool authAccepted = SimulatePrompt("Manual authorization required. Enter code.", inputSpec, out authCode);

                                if (!IsPaymentSessionActive)
                                    return;

                                if (!authAccepted || string.IsNullOrEmpty(authCode))
                                {
                                    AddSessionStatus(new TerminalStatus
                                    {
                                        State = TerminalState.Idle,
                                        IsConnected = isConnected,
                                        LastResultIsFinal = true,
                                        ActiveOperationId = operationId,
                                        ActiveOperationType = OperationType.None,
                                        ClientReference = clientReference,
                                        UpdatedAt = DateTimeOffset.UtcNow,
                                        Message = "Transaction aborted: manual authorization declined or timed out.",
                                        Fault = new FaultInfo { Code = "PromptDeclined", Message = "Manual authorization declined or timed out." },
                                    });
                                    return;
                                }
                            }

                            // Terminal aborted scenario — terminal cancels at cardholder verification.
                            if (scenario == PurchaseScenario.TerminalAborted)
                            {
                                AddSessionStatus(new TerminalStatus
                                {
                                    State = TerminalState.Aborted,
                                    IsConnected = isConnected,
                                    LastResultIsFinal = true,
                                    ActiveOperationId = operationId,
                                    ActiveOperationType = OperationType.None,
                                    ClientReference = clientReference,
                                    UpdatedAt = DateTimeOffset.UtcNow,
                                    Message = "Transaction aborted by terminal during cardholder verification.",
                                    Fault = new FaultInfo { Code = "1013", Message = "Timeout during application selection or PIN." },
                                });
                                return;
                            }
                        }
                    }

                    if (!IsPaymentSessionActive)
                        return;

                    VendorPayload bonusVendorPayload = null;
                    string bonusSummary = string.Empty;

                    if (withBonus)
                    {
                        string customerNumber = $"TEST{random.Next(10000, 99999)}";
                        string memberClass = random.Next(0, 2) == 0 ? "Gold" : "Silver";

                        bonusSummary = $" Customer={customerNumber} MemberClass={memberClass}";
                        bonusVendorPayload = new VendorPayload();
                        bonusVendorPayload.AdditionalProperties["bonusCustomerNumber"] = customerNumber;
                        bonusVendorPayload.AdditionalProperties["bonusMemberClass"] = memberClass;
                        bonusVendorPayload.AdditionalProperties["bonusStatusCode"] = string.Empty;
                        bonusVendorPayload.AdditionalProperties["bonusStatusText"] = "Bonus card accepted.";
                    }

                    var resultPayload = new VendorPayload();
                    resultPayload.AdditionalProperties["messageId"] = "0";
                    resultPayload.AdditionalProperties["transactionId"] = random.Next(10000, 99999).ToString();
                    resultPayload.AdditionalProperties["transactionType"] = "Purchase";
                    resultPayload.AdditionalProperties["paymentMethod"] = isChipPin ? "Chip" : "Contactless";
                    resultPayload.AdditionalProperties["amount"] = amount;
                    resultPayload.AdditionalProperties["currency"] = currency;
                    resultPayload.AdditionalProperties["flags"] = "00";
                    resultPayload.AdditionalProperties["transactionDateTime"] = DateTimeOffset.UtcNow;

                    if (!string.IsNullOrEmpty(clientReference))
                        resultPayload.AdditionalProperties["clientReference"] = clientReference;

                    if (bonusVendorPayload != null)
                    {
                        foreach (var pair in bonusVendorPayload.AdditionalProperties)
                            resultPayload.AdditionalProperties[pair.Key] = pair.Value;
                    }

                    AddSessionStatus(new TerminalStatus
                    {
                        State = TerminalState.Completed,
                        IsConnected = isConnected,
                        LastResultIsFinal = true,
                        ActiveOperationId = operationId,
                        ActiveOperationType = OperationType.None,
                        ClientReference = clientReference,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        Message = $"Transaction completed.{bonusSummary}",
                        OperationResult = new OperationResult { VendorPayload = resultPayload },
                    });
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{nameof(SimulatePurchaseAsync)} failed: {ex}", GetType().FullName);
                }
            });
        }

        private void SimulateSimpleTransactionAsync(string operationLabel, string transactionType, long amount, string currency)
        {
            string operationId = CurrentStatus?.ActiveOperationId;
            string clientReference = CurrentStatus?.ClientReference;
            bool isConnected = CurrentStatus?.IsConnected ?? false;
            int roll = random.Next(0, 100);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    foreach (var phase in SimulatedSimplePhases)
                    {
                        Thread.Sleep((int)phase[2]);

                        if (!IsPaymentSessionActive)
                            return;

                        if ((string)phase[0] == "8" && roll < ConnectionErrorThreshold)
                        {
                            AddSessionStatus(new TerminalStatus
                            {
                                State = TerminalState.Error,
                                IsConnected = false,
                                LastResultIsFinal = true,
                                ActiveOperationId = operationId,
                                ActiveOperationType = OperationType.None,
                                ClientReference = clientReference,
                                UpdatedAt = DateTimeOffset.UtcNow,
                                Message = $"{operationLabel} failed: connection lost during authorization.",
                                Fault = new FaultInfo { Code = "TerminalDisconnected", Message = "Connection lost during authorization." },
                            });
                            return;
                        }

                        AddSessionStatus(new TerminalStatus
                        {
                            State = (TerminalState)phase[3],
                            IsConnected = isConnected,
                            LastResultIsFinal = false,
                            ActiveOperationId = operationId,
                            ActiveOperationType = OperationType.None,
                            ClientReference = clientReference,
                            UpdatedAt = DateTimeOffset.UtcNow,
                            Message = $"{(string)phase[1]} {amount / 100m:F2} {currency}".Trim(),
                        });
                    }

                    if (!IsPaymentSessionActive)
                        return;

                    if (roll < ConnectionErrorThreshold + DeclinedThreshold)
                    {
                        var declineCode = DeclineCodes[random.Next(0, DeclineCodes.Length)];

                        var declinePayload = new VendorPayload();
                        declinePayload.AdditionalProperties["messageId"] = declineCode[0];
                        declinePayload.AdditionalProperties["transactionType"] = transactionType;
                        declinePayload.AdditionalProperties["amount"] = amount;
                        declinePayload.AdditionalProperties["currency"] = currency;

                        if (!string.IsNullOrEmpty(clientReference))
                            declinePayload.AdditionalProperties["clientReference"] = clientReference;

                        AddSessionStatus(new TerminalStatus
                        {
                            State = TerminalState.Idle,
                            IsConnected = isConnected,
                            LastResultIsFinal = true,
                            ActiveOperationId = operationId,
                            ActiveOperationType = OperationType.None,
                            ClientReference = clientReference,
                            UpdatedAt = DateTimeOffset.UtcNow,
                            Message = $"Transaction failed: ({declineCode[0]}) {declineCode[1]} {amount / 100m:F2} {currency}",
                            OperationResult = new OperationResult { VendorPayload = declinePayload },
                            Fault = new FaultInfo
                            {
                                Code = declineCode[0],
                                Message = $"Transaction declined or failed. MessageId={declineCode[0]}",
                            },
                        });
                        return;
                    }

                    var resultPayload = new VendorPayload();
                    resultPayload.AdditionalProperties["messageId"] = "0";
                    resultPayload.AdditionalProperties["transactionId"] = random.Next(10000, 99999).ToString();
                    resultPayload.AdditionalProperties["transactionType"] = transactionType;
                    resultPayload.AdditionalProperties["amount"] = amount;
                    resultPayload.AdditionalProperties["currency"] = currency;
                    resultPayload.AdditionalProperties["transactionDateTime"] = DateTimeOffset.UtcNow;

                    if (!string.IsNullOrEmpty(clientReference))
                        resultPayload.AdditionalProperties["clientReference"] = clientReference;

                    AddSessionStatus(new TerminalStatus
                    {
                        State = TerminalState.Completed,
                        IsConnected = isConnected,
                        LastResultIsFinal = true,
                        ActiveOperationId = operationId,
                        ActiveOperationType = OperationType.None,
                        ClientReference = clientReference,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        Message = $"Transaction completed. {amount / 100m:F2} {currency}",
                        OperationResult = new OperationResult { VendorPayload = resultPayload },
                    });
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{nameof(SimulateSimpleTransactionAsync)} ({operationLabel}) failed: {ex}", GetType().FullName);
                }
            });
        }

        // Guarantees every scenario type appears in each cycle of 10 purchases,
        // in a randomized order. Refills and reshuffles automatically when exhausted.
        private readonly Queue<PurchaseScenario> purchaseScenarioQueue = new Queue<PurchaseScenario>();

        private enum PurchaseScenario
        {
            Success,
            ConnectionError,
            PromptYesNo,
            PromptInput,
            TerminalAborted,
        }

        private PurchaseScenario DequeuePurchaseScenario()
        {
            if (purchaseScenarioQueue.Count == 0)
                RefillPurchaseScenarioQueue();

            return purchaseScenarioQueue.Dequeue();
        }

        private void RefillPurchaseScenarioQueue()
        {
            // Two of each scenario type — 10 total per cycle.
            var scenarios = new List<PurchaseScenario>
            {
                PurchaseScenario.Success,
                PurchaseScenario.Success,
                PurchaseScenario.ConnectionError,
                PurchaseScenario.ConnectionError,
                PurchaseScenario.PromptYesNo,
                PurchaseScenario.PromptYesNo,
                PurchaseScenario.PromptInput,
                PurchaseScenario.PromptInput,
                PurchaseScenario.TerminalAborted,
                PurchaseScenario.TerminalAborted,
            };

            // Fisher-Yates shuffle.
            for (int i = scenarios.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                PurchaseScenario temp = scenarios[i];
                scenarios[i] = scenarios[j];
                scenarios[j] = temp;
            }

            foreach (var scenario in scenarios)
                purchaseScenarioQueue.Enqueue(scenario);
        }

        /// <summary>
        /// Publishes an <see cref="TerminalState.AwaitingPrompt"/> status and blocks the calling
        /// simulation thread until the POS responds via <see cref="DoRespondToPromptAsync"/> or
        /// <see cref="PromptTimeoutSeconds"/> elapses. Returns <c>false</c> on timeout or decline.
        /// </summary>
        /// <param name="message">The prompt message shown to the operator.</param>
        /// <param name="inputSpec">
        /// Input constraints for free-form entry. When <c>null</c> the prompt is yes/no only.
        /// </param>
        /// <param name="input">
        /// On return, contains the operator-entered text when <paramref name="inputSpec"/> is set;
        /// otherwise <c>null</c>.
        /// </param>
        private bool SimulatePrompt(string message, PromptInputSpec inputSpec, out string input)
        {
            string promptId = "pr_" + Guid.NewGuid().ToString("N");
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(PromptTimeoutSeconds);

            var prompt = new Prompt
            {
                PromptId = promptId,
                Message = message,
                YesNo = inputSpec == null,
                Input = inputSpec,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = expiresAt,
            };

            var pending = new PendingPrompt(promptId);
            pendingPrompt = pending;

            var currentStatus = CurrentStatus;

            AddSessionStatus(new TerminalStatus
            {
                State = TerminalState.AwaitingPrompt,
                IsConnected = currentStatus.IsConnected,
                LastResultIsFinal = false,
                ActiveOperationId = currentStatus.ActiveOperationId,
                ActiveOperationType = currentStatus.ActiveOperationType,
                ClientReference = currentStatus.ClientReference,
                UpdatedAt = DateTimeOffset.UtcNow,
                Message = message,
                Prompt = prompt,
            });

            bool accepted = pending.Wait(PromptTimeoutSeconds * 1000, out input);
            pendingPrompt = null;
            return accepted;
        }

        /// <summary>
        /// Convenience overload for yes/no prompts with no text input.
        /// </summary>
        private bool SimulatePrompt(string message)
        {
            string ignored;
            return SimulatePrompt(message, null, out ignored);
        }

        /// <summary>
        /// Holds the state for a prompt that is blocking a simulation thread
        /// while awaiting a REST response from the POS.
        /// </summary>
        private sealed class PendingPrompt
        {
            private readonly ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);
            private bool accepted;
            private string input;

            public string PromptId { get; }

            public PendingPrompt(string promptId)
            {
                PromptId = promptId;
            }

            /// <summary>Called by the REST handler to deliver the POS response.</summary>
            public void Reply(bool isAccepted, string value)
            {
                accepted = isAccepted;
                input = value;
                resetEvent.Set();
            }

            /// <summary>Unblocks the waiting thread with a declined result (used on abort or dispose).</summary>
            public void Cancel()
            {
                resetEvent.Set();
            }

            /// <summary>
            /// Blocks until a reply arrives or <paramref name="timeoutMilliseconds"/> elapses.
            /// Also returns the operator-entered input text. Returns <c>false</c> on timeout.
            /// </summary>
            public bool Wait(int timeoutMilliseconds, out string value)
            {
                resetEvent.Wait(timeoutMilliseconds);
                value = input;
                return accepted;
            }
        }
    }
}
