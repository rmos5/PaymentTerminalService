using PaymentTerminalService.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace PaymentTerminalService.Terminals
{
    internal partial class TestPaymentTerminal
    {
        // Probability thresholds out of 100.
        private const int DeclinedThreshold = 15;        // 15 % transaction declined
        private const int ConnectionErrorThreshold = 15; // 15 % connection lost during auth
        private const int AbortRejectedThreshold = 20;   // 20 % abort rejected by terminal
        private const int AbortExceptionThreshold = 10;      // 10% abort throws exception
        private const int AbortPersistentRejectionThreshold = 5;  // 5% abort ALWAYS rejects (tests timeout)
        private const int BonusReadFailedThreshold = 20; // 20 % bonus card read fails

        // Orphan result simulation: fires between 20 and 60 seconds after the previous orphan.
        private const int OrphanMinDelaySeconds = 500;
        private const int OrphanMaxDelaySeconds = 3000;

        private readonly Random random = new Random();
        private Timer orphanSimulationTimer;

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

        // Guarantees every scenario type appears in each cycle, in a randomized order.
        // Refills and reshuffles automatically when exhausted.
        private readonly Queue<PurchaseScenario> purchaseScenarioQueue = new Queue<PurchaseScenario>();

        private enum PurchaseScenario
        {
            Success,
            ConnectionError,
            PromptYesNo,
            PromptInput,
            TerminalAborted,
        }

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

        private PurchaseScenario DequeuePurchaseScenario()
        {
            if (purchaseScenarioQueue.Count == 0)
                RefillPurchaseScenarioQueue();

            return purchaseScenarioQueue.Dequeue();
        }

        private void RefillPurchaseScenarioQueue()
        {
            // Scenario distribution per cycle:
            //   4× ConnectionError (~33 %) — elevated to stress-test error handling
            //   2× Success
            //   2× PromptYesNo
            //   2× PromptInput
            //   2× TerminalAborted
            var scenarios = new List<PurchaseScenario>
            {
                PurchaseScenario.Success,
                PurchaseScenario.Success,
                PurchaseScenario.ConnectionError,
                PurchaseScenario.ConnectionError,
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

            // Generate a stable transaction ID for this run so the "A" (initialized) status
            // and the final result payload carry the same transactionId, mirroring the real terminal.
            string simulatedTransactionId = random.Next(10000, 99999).ToString();
            DateTime simulatedTransactionDateTime = DateTime.UtcNow;

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

                        // Transaction initialized — attach transactionId and transactionDateTime,
                        // mirroring TerminalManager_TransactionInitialized in VerifoneYomaniXRTerminal.
                        VendorPayload phaseVendorPayload = null;
                        if ((string)phase[0] == "A")
                        {
                            phaseVendorPayload = new VendorPayload
                            {
                                AdditionalProperties =
                                {
                                    [VendorPayloadKeys.TransactionId] = simulatedTransactionId,
                                    [VendorPayloadKeys.TransactionDateTime] = simulatedTransactionDateTime,
                                }
                            };
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
                            VendorPayload = phaseVendorPayload,
                        });

                        if ((string)phase[0] == "7")
                        {
                            // Yes/No prompt scenario — PIN bypass confirmation.
                            if (scenario == PurchaseScenario.PromptYesNo)
                            {
                                bool promptAccepted = SimulatePrompt("PIN bypass requested. Confirm?");

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
                    resultPayload.AdditionalProperties[VendorPayloadKeys.TransactionId] = simulatedTransactionId;
                    resultPayload.AdditionalProperties["transactionType"] = "Purchase";
                    resultPayload.AdditionalProperties["paymentMethod"] = isChipPin ? "Chip" : "Contactless";
                    resultPayload.AdditionalProperties["amount"] = amount;
                    resultPayload.AdditionalProperties["currency"] = currency;
                    resultPayload.AdditionalProperties["flags"] = "00";
                    resultPayload.AdditionalProperties[VendorPayloadKeys.TransactionDateTime] = simulatedTransactionDateTime;

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
                    Trace.WriteLine($"{nameof(SimulatePurchaseAsync)}:\n{ex}", GetType().FullName);
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
                    Trace.WriteLine($"{nameof(SimulateSimpleTransactionAsync)}:\n{ex}", GetType().FullName);
                }
            });
        }

        private OperationResult BuildOrphanResult()
        {
            var payload = new VendorPayload();
            payload.AdditionalProperties["messageId"] = "0";
            payload.AdditionalProperties["transactionId"] = random.Next(10000, 99999).ToString();
            payload.AdditionalProperties["transactionType"] = "Purchase";
            payload.AdditionalProperties["paymentMethod"] = random.Next(0, 2) == 0 ? "Chip" : "Contactless";
            payload.AdditionalProperties["amount"] = (long)random.Next(100, 20000);
            payload.AdditionalProperties["currency"] = "EUR";
            payload.AdditionalProperties["transactionDateTime"] = DateTimeOffset.UtcNow;
            return new OperationResult { VendorPayload = payload };
        }

        private void ScheduleNextOrphan()
        {
            int delaySeconds = random.Next(OrphanMinDelaySeconds, OrphanMaxDelaySeconds + 1);
            Trace.WriteLine($"{nameof(ScheduleNextOrphan)}: next orphan in {delaySeconds}s", GetType().FullName);

            orphanSimulationTimer?.Dispose();
            orphanSimulationTimer = new Timer(OnOrphanTimerFired, null,
                delaySeconds * 1000, Timeout.Infinite);
        }

        private void OnOrphanTimerFired(object state)
        {
            try
            {
                var orphan = new TerminalStatus
                {
                    State = TerminalState.Completed,
                    IsConnected = CurrentStatus?.IsConnected ?? false,
                    LastResultIsFinal = true,
                    ActiveOperationId = "orphan_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Orphan transaction result received from terminal.",
                    OperationResult = BuildOrphanResult(),
                };

                Trace.WriteLine($"{nameof(OnOrphanTimerFired)}: delivering orphan operationId={orphan.ActiveOperationId}", GetType().FullName);
                HandleOrphanStatus(orphan);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(OnOrphanTimerFired)}:\n{ex}", GetType().FullName);
            }
            finally
            {
                ScheduleNextOrphan();
            }
        }

        /// <summary>
        /// Starts the orphan result simulation timer with a randomized initial delay.
        /// Call once after the terminal is initialized.
        /// </summary>
        internal void StartOrphanSimulation()
        {
            Trace.WriteLine($"{nameof(StartOrphanSimulation)}", GetType().FullName);
            ScheduleNextOrphan();
        }

        /// <summary>
        /// Stops the orphan simulation timer and releases its resources.
        /// </summary>
        internal void StopOrphanSimulation()
        {
            Trace.WriteLine($"{nameof(StopOrphanSimulation)}", GetType().FullName);

            var timer = orphanSimulationTimer;
            orphanSimulationTimer = null;
            timer?.Dispose();
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

            /// <summary>Unblocks the waiting thread with a declined result (used on abort, error, or dispose).</summary>
            public void Cancel()
            {
                resetEvent.Set();
            }

            /// <summary>
            /// Blocks until a reply arrives or <paramref name="timeoutMilliseconds"/> elapses.
            /// Also returns the operator-entered input text. Returns <c>false</c> on timeout or cancel.
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