using PaymentTerminalService.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Verifone.ECRTerminal;

namespace PaymentTerminalService.Terminals
{
    /// <summary>
    /// Represents a payment terminal implementation for the Verifone Yomani XR device.
    /// Handles terminal operations and user prompts using the Verifone ECRTerminal API.
    /// </summary>
    internal sealed class VerifoneYomaniXRTerminal : PaymentTerminalBase, IUserPromptHandler
    {
        public const string VendorString = "YOMANI";
        public const string ModelString = "Yomani XR";

        public const string TraceSerialBytesPropertyName = "traceSerialBytes";
        public const string EnableManualAuthorizationPropertyName = "enableManualAuthorization";

        /// <inheritdoc/>
        public override string Vendor => VendorString;

        /// <inheritdoc/>
        public override string Model => ModelString;

        /// <summary>
        /// Gets a value indicating whether raw serial bytes are written to the trace output.
        /// Controlled via <c>traceSerialBytes</c> in the constructor <see cref="PaymentTerminalBase.VendorPayload"/>
        /// </summary>
        public bool TraceSerialBytes { get; }

        /// <summary>
        /// Gets the ECR terminal manager used to communicate with the hardware.
        /// </summary>
        protected IECRTerminalManager TerminalManager { get; }

        /// <summary>
        /// Gets a value indicating whether manual authorization is enabled.
        /// When <see langword="true"/>, the terminal may raise a free-form input prompt
        /// during authorization. Controlled via <c>enableManualAuthorization</c> in the
        /// constructor <see cref="PaymentTerminalBase.VendorPayload"/>.
        /// </summary>
        public bool EnableManualAuthorization { get; }

        // Active prompt blocking the ECR protocol thread, awaiting REST response via DoRespondToPromptAsync.
        private volatile PendingPrompt pendingPrompt;

        private void AddTerminalManagerEvents()
        {
            TerminalManager.WakeupECRReceived += TerminalManager_WakeupEcrReceived;
            TerminalManager.DeviceControlResultReceived += TerminalManager_DeviceControlResultReceived;
            TerminalManager.TransactionStatusChanged += TerminalManager_TransactionStatusChanged;
            TerminalManager.TransactionInitialized += TerminalManager_TransactionInitialized;
            TerminalManager.TransactionTerminalAbortReceived += TerminalManager_TransactionTerminalAbortReceived;
            TerminalManager.PurchaseCreated += TerminalManager_PurchaseCreated;
            TerminalManager.TransactionResultReceived += TerminalManager_TransactionResultReceived;
            TerminalManager.ReversalCreated += TerminalManager_ReversalCreated;
            TerminalManager.RefundCreated += TerminalManager_RefundCreated;
            TerminalManager.BonusResultReceived += TerminalManager_BonusResultReceived;
            TerminalManager.AbortTransactionResultReceived += TerminalManager_AbortTransactionResultReceived;
            TerminalManager.TerminalCommandAccepted += TerminalManager_TerminalCommandAccepted;
            TerminalManager.TerminalError += TerminalManager_TerminalError;
        }

        private void RemoveTerminalManagerEvents()
        {
            TerminalManager.WakeupECRReceived -= TerminalManager_WakeupEcrReceived;
            TerminalManager.DeviceControlResultReceived -= TerminalManager_DeviceControlResultReceived;
            TerminalManager.TransactionStatusChanged -= TerminalManager_TransactionStatusChanged;
            TerminalManager.TransactionInitialized -= TerminalManager_TransactionInitialized;
            TerminalManager.TransactionTerminalAbortReceived -= TerminalManager_TransactionTerminalAbortReceived;
            TerminalManager.PurchaseCreated -= TerminalManager_PurchaseCreated;
            TerminalManager.TransactionResultReceived -= TerminalManager_TransactionResultReceived;
            TerminalManager.ReversalCreated -= TerminalManager_ReversalCreated;
            TerminalManager.RefundCreated -= TerminalManager_RefundCreated;
            TerminalManager.BonusResultReceived -= TerminalManager_BonusResultReceived;
            TerminalManager.AbortTransactionResultReceived -= TerminalManager_AbortTransactionResultReceived;
            TerminalManager.TerminalCommandAccepted -= TerminalManager_TerminalCommandAccepted;
            TerminalManager.TerminalError -= TerminalManager_TerminalError;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VerifoneYomaniXRTerminal"/> class.
        /// </summary>
        /// <param name="terminalId">Unique identifier for the terminal.</param>
        /// <param name="displayName">Display name for the terminal.</param>
        /// <param name="connection">Serial connection options. Must have a valid <c>Serial.PortName</c>.</param>
        /// <param name="isLoyaltySupported">Indicates whether loyalty operations are supported.</param>
        /// <param name="vendorPayload">
        /// Vendor-specific configuration. Recognized properties: <c>traceSerialBytes</c>, <c>enableManualAuthorization</c>.
        /// </param>
        /// <param name="sessionStorageProvider">Optional session storage provider for persisting session data.</param>
        public VerifoneYomaniXRTerminal(
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
            TraceSerialBytes = vendorPayload?.AdditionalProperties.GetPropertyOrDefault<bool>(TraceSerialBytesPropertyName) ?? false;
            EnableManualAuthorization = vendorPayload?.AdditionalProperties
                .GetPropertyOrDefault<bool>(EnableManualAuthorizationPropertyName)
                ?? false;

            TerminalManager = new ECRTerminalManager2(
                connection.Serial.PortName,
                this,
                EnableManualAuthorization,
                traceSerialBytes: TraceSerialBytes);

            AddTerminalManagerEvents();
        }

        /// <inheritdoc/>
        protected override async Task UpdateDeviceInfoAsync()
        {
            var result = await TerminalManager.WaitForCommandResultAsync<DeviceControlResultEventArgs>(
                CommandId.RequestTerminalVersion,
                () => TerminalManager.RequestTerminalVersion(),
                handler => TerminalManager.DeviceControlResultReceived += handler,
                handler => TerminalManager.DeviceControlResultReceived -= handler,
                eventArgs => eventArgs?.DeviceStatus != null,
                CancellationToken.None).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result.DeviceStatus.Data))
                throw new InvalidOperationException("Terminal version response did not contain version data.");

            Version = result.DeviceStatus.Data.Trim();
        }

        private void TerminalManager_WakeupEcrReceived(object sender, EventArgs e)
            => Trace.WriteLine($"{nameof(TerminalManager_WakeupEcrReceived)}", GetType().FullName);

        private void TerminalManager_DeviceControlResultReceived(object sender, DeviceControlResultEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_DeviceControlResultReceived)}: {e}", GetType().FullName);

            var deviceStatus = e.DeviceStatus;
            var currentStatus = CurrentStatus;

            var vendorPayload = new VendorPayload();
            vendorPayload.AdditionalProperties["terminalResultCode"] = deviceStatus.ResultCode;
            vendorPayload.AdditionalProperties["terminalResultMessage"] = deviceStatus.ResultCodeMessage;
            vendorPayload.AdditionalProperties["readerStatus"] = deviceStatus.ReaderStatus;
            vendorPayload.AdditionalProperties["readerStatusMessage"] = deviceStatus.ReaderStatusMessage;
            vendorPayload.AdditionalProperties["environment"] = deviceStatus.Environment;
            vendorPayload.AdditionalProperties["environmentMessage"] = deviceStatus.EnvironmentMessage;
            vendorPayload.AdditionalProperties["tcsMessagePresent"] = deviceStatus.IsTCSMessagePresent;

            if (!string.IsNullOrWhiteSpace(deviceStatus.Data))
                vendorPayload.AdditionalProperties["data"] = deviceStatus.Data.Trim();

            var updated = new TerminalStatus
            {
                State = currentStatus.State,
                IsConnected = currentStatus.IsConnected,
                LastResultIsFinal = currentStatus.LastResultIsFinal,
                ActiveOperationId = currentStatus.ActiveOperationId,
                ActiveOperationType = currentStatus.ActiveOperationType,
                ClientReference = currentStatus.ClientReference,
                SessionName = currentStatus.SessionName,
                UpdatedAt = DateTimeOffset.UtcNow,
                Message = currentStatus.Message,
                Prompt = currentStatus.Prompt,
                OperationResult = currentStatus.OperationResult,
                Fault = currentStatus.Fault,
                VendorPayload = vendorPayload,
            };

            UpdateCurrentStatus(updated);
        }

        private void TerminalManager_TerminalCommandAccepted(object sender, TerminalCommandAcceptedEventArgs e)
            => Trace.WriteLine($"{nameof(TerminalManager_TerminalCommandAccepted)}: {e}", GetType().FullName);

        private void TerminalManager_TerminalError(object sender, ExceptionEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_TerminalError)}: {e.Exception}", GetType().FullName);
            CancelPendingPrompt();
            HandleTerminalError(e.Exception);
        }

        private void TerminalManager_TransactionStatusChanged(object sender, TransactionStatusEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_TransactionStatusChanged)}: {e}", GetType().FullName);

            var statusMessage = $"{e.StatusPhaseMessage} {e.StatusResultCodeMessage}".Trim();
            var currentStatus = CurrentStatus;
            bool isSessionActive = IsPaymentSessionActive;

            if (isSessionActive)
            {
                AddSessionStatus(new TerminalStatus
                {
                    State = TerminalState.TransactionInProgress,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = false,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = currentStatus.ActiveOperationType,
                    ClientReference = currentStatus.ClientReference,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = statusMessage,
                });
            }
            else
            {
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = statusMessage,
                });
            }
        }

        private void TerminalManager_TransactionInitialized(object sender, TransactionEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_TransactionInitialized)}: {e}", GetType().FullName);
            var currentStatus = CurrentStatus;

            if (IsPaymentSessionActive)
            {
                AddSessionStatus(new TerminalStatus
                {
                    State = TerminalState.TransactionInProgress,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = false,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = currentStatus.ActiveOperationType,
                    ClientReference = currentStatus.ClientReference,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Transaction initialized. Id={e.TransactionId} At={e.TransactionDateTime:G}",
                });
            }
            else
            {
                // Transaction initialized outside any managed session — unsolicited hardware event.
                // Surfaced as a diagnostic currentStatus so the POS can detect unexpected terminal activity.
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.TransactionInProgress,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = false,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Unsolicited transaction initialized outside session. Id={e.TransactionId} At={e.TransactionDateTime:G}",
                });
            }
        }

        private void TerminalManager_TransactionTerminalAbortReceived(object sender, TransactionStatusEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_TransactionTerminalAbortReceived)}: {e}", GetType().FullName);

            CancelPendingPrompt();

            var currentStatus = CurrentStatus;

            if (IsPaymentSessionActive)
            {
                AddSessionStatus(new TerminalStatus
                {
                    State = TerminalState.Aborted,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = OperationType.None,
                    ClientReference = currentStatus.ClientReference,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Terminal aborted: {e.StatusResultCodeMessage}",
                    Fault = new FaultInfo { Code = e.StatusResultCode, Message = e.StatusResultCodeMessage },
                });
            }
            else
            {
                // Spurious terminal abort received outside any managed session.
                // Record as a fault on currentStatus currentStatus so the POS can detect and reconcile if needed.
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Aborted,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Spurious terminal abort received outside session: {e.StatusResultCodeMessage}",
                    Fault = new FaultInfo { Code = e.StatusResultCode, Message = e.StatusResultCodeMessage },
                });
            }
        }

        private void TerminalManager_BonusResultReceived(object sender, CustomerRequestResultEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_BonusResultReceived)}: {e}", GetType().FullName);

            bool isFailed = string.IsNullOrEmpty(e.CustomerNumber?.Trim());
            var currentStatus = CurrentStatus;

            if (isFailed)
            {
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Bonus card read failed.",
                    Fault = new FaultInfo { Code = e.StatusCode, Message = e.StatusText },
                });
            }
            else
            {
                VendorPayload vendorPayload = new VendorPayload();
                UpdateBonusInfo(e, vendorPayload);
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Idle,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = OperationType.LoyaltyActivate,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Bonus card read successful.",
                    OperationResult = new OperationResult
                    {
                        VendorPayload = vendorPayload
                    }
                });
            }
        }

        private void TerminalManager_PurchaseCreated(object sender, TransactionResultEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_PurchaseCreated)}: {e}", GetType().FullName);
            HandleTransactionResult(e, false);
        }

        private void TerminalManager_ReversalCreated(object sender, TransactionResultEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_ReversalCreated)}: {e}", GetType().FullName);
            HandleTransactionResult(e, false);
        }

        private void TerminalManager_RefundCreated(object sender, TransactionResultEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_RefundCreated)}: {e}", GetType().FullName);
            HandleTransactionResult(e, false);
        }

        private void TerminalManager_TransactionResultReceived(object sender, TransactionResultEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_TransactionResultReceived)}: {e}", GetType().FullName);
            HandleTransactionResult(e, true);
        }

        private void TerminalManager_AbortTransactionResultReceived(object sender, AbortTransactionResultEventArgs e)
        {
            Trace.WriteLine($"{nameof(TerminalManager_AbortTransactionResultReceived)}: {e}", GetType().FullName);

            var currentStatus = CurrentStatus;
            TerminalStatus terminalStatus;

            if (e.IsAborted)
            {
                terminalStatus = new TerminalStatus
                {
                    State = TerminalState.Aborted,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = OperationType.None,
                    ClientReference = currentStatus.ClientReference,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = e.Message,
                };
            }
            else
            {
                // Terminal rejected abort (e.g. authorization in progress).
                // Session stays open — caller may retry AbortTransactionAsync.
                terminalStatus = new TerminalStatus
                {
                    State = currentStatus.State,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = false,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = currentStatus.ActiveOperationType,
                    ClientReference = currentStatus.ClientReference,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = e.Message,
                    Fault = new FaultInfo { Code = e.ResultCode, Message = e.Message },
                };
            }

            if (IsPaymentSessionActive)
            {
                AddSessionStatus(terminalStatus);
            }
            else
            {
                UpdateCurrentStatus(terminalStatus);
            }
        }

        /// <summary>
        /// Builds a <see cref="VendorPayload"/> from the extended transaction result returned by the ECR terminal.
        /// Includes all transaction fields reported by the hardware and, when available, the path of the raw
        /// transaction file persisted by the ECR manager. The file path is recorded here so it survives
        /// in the session storage and can be cleaned up or referenced after the session is confirmed.
        /// </summary>
        /// <param name="transactionInfo">The extended transaction result from the ECR protocol.</param>
        /// <param name="transactionFilePath">
        /// Optional path of the raw transaction file written by <see cref="ECRTerminalManager"/>.
        /// When non-empty, included as <c>transactionFilePath</c> in the payload and deleted after
        /// the session is persisted by <see cref="ISessionStorageProvider"/>.
        /// </param>
        /// <param name="clientReference">
        /// The POS correlation id originally supplied with the request. When non-empty, included as
        /// <c>clientReference</c> in the payload so the result can be correlated back to its request.
        /// </param>
        /// <returns>A populated <see cref="VendorPayload"/> containing all transaction fields.</returns>
        private VendorPayload BuildTransactionVendorPayload(TransactionResultExEventArgs transactionInfo, string transactionFilePath, string clientReference)
        {
            var payload = new VendorPayload();

            payload.AdditionalProperties["messageId"] = transactionInfo.MessageId;
            payload.AdditionalProperties["transactionId"] = transactionInfo.TransactionId;
            payload.AdditionalProperties["transactionType"] = transactionInfo.TransactionType;
            payload.AdditionalProperties["paymentMethod"] = transactionInfo.PaymentMethod;
            payload.AdditionalProperties["cardType"] = transactionInfo.CardType;
            payload.AdditionalProperties["transactionUsage"] = transactionInfo.TransactionUsage;
            payload.AdditionalProperties["settlementId"] = transactionInfo.SettlementId;
            payload.AdditionalProperties["maskedCardNumber"] = transactionInfo.MaskedCardNumber;
            payload.AdditionalProperties["aid"] = transactionInfo.Aid;
            payload.AdditionalProperties["transactionCertificate"] = transactionInfo.TransactionCertificate;
            payload.AdditionalProperties["tvr"] = transactionInfo.Tvr;
            payload.AdditionalProperties["tsi"] = transactionInfo.Tsi;
            payload.AdditionalProperties["filingCode"] = transactionInfo.FilingCode;
            payload.AdditionalProperties["transactionDateTime"] = transactionInfo.TransactionDateTime;
            payload.AdditionalProperties["amount"] = transactionInfo.Amount;
            payload.AdditionalProperties["currency"] = transactionInfo.Currency;
            payload.AdditionalProperties["readerSerialNumber"] = transactionInfo.ReaderSerialNumber;
            payload.AdditionalProperties["printPayeeReceipt"] = transactionInfo.PrintPayeeReceipt;
            payload.AdditionalProperties["flags"] = transactionInfo.Flags;
            payload.AdditionalProperties["payerReceiptText"] = transactionInfo.PayerReceiptText;
            payload.AdditionalProperties["payeeReceiptText"] = transactionInfo.PayeeReceiptText;

            if (!string.IsNullOrEmpty(clientReference))
                payload.AdditionalProperties["clientReference"] = clientReference;

            if (!string.IsNullOrWhiteSpace(transactionFilePath))
                payload.AdditionalProperties["transactionFilePath"] = transactionFilePath;

            return payload;
        }

        /// <summary>
        /// Merges bonus card fields from <paramref name="bonusInfo"/> into <paramref name="vendorPayload"/>.
        /// </summary>
        /// <param name="bonusInfo">The bonus result received from the ECR terminal.</param>
        /// <param name="vendorPayload">The payload to write bonus fields into.</param>
        private void UpdateBonusInfo(CustomerRequestResultEventArgs bonusInfo, VendorPayload vendorPayload)
        {
            vendorPayload.AdditionalProperties["bonusCustomerNumber"] = bonusInfo.CustomerNumber;
            vendorPayload.AdditionalProperties["bonusMemberClass"] = bonusInfo.MemberClass;
            vendorPayload.AdditionalProperties["bonusStatusCode"] = bonusInfo.StatusCode;
            vendorPayload.AdditionalProperties["bonusStatusText"] = bonusInfo.StatusText;
        }

        /// <summary>
        /// Deletes the ECR manager's raw transaction file after the session has been
        /// persisted by <see cref="ISessionStorageProvider"/>, preventing duplicate storage.
        /// Safe to call with a <see langword="null"/> or missing path.
        /// </summary>
        /// <param name="transactionFilePath">The file path from <see cref="TransactionResultEventArgs.TransactionFilePath"/>.</param>
        private void DeleteTransactionFile(string transactionFilePath)
        {
            try
            {
                if (File.Exists(transactionFilePath))
                {
                    File.Delete(transactionFilePath);
                    Trace.WriteLine($"{nameof(DeleteTransactionFile)}: deleted '{transactionFilePath}'", GetType().FullName);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(DeleteTransactionFile)}: failed to delete '{transactionFilePath}': {ex.Message}", GetType().FullName);
            }
        }

        /// <summary>
        /// Processes a transaction result from the ECR terminal manager.
        /// When <paramref name="isOutsideTerminalManagerSession"/> is <see langword="true"/>, the result
        /// arrived outside any active managed session (e.g. a spurious or externally-initiated transaction)
        /// and is recorded as a <see cref="TerminalState.Error"/> currentStatus to flag that manual reconciliation
        /// may be required. Otherwise the result is treated as the final outcome of the currentStatus session.
        /// </summary>
        /// <param name="e">The transaction result event arguments.</param>
        /// <param name="isOutsideTerminalManagerSession">
        /// <see langword="true"/> if the result arrived outside a managed session; otherwise <see langword="false"/>.
        /// </param>
        private void HandleTransactionResult(TransactionResultEventArgs e, bool isOutsideTerminalManagerSession)
        {
            Trace.WriteLine($"{nameof(HandleTransactionResult)}: isOutsideTerminalManagerSession={isOutsideTerminalManagerSession} {e}", GetType().FullName);

            var currentStatus = CurrentStatus;
            bool isSessionActive = IsPaymentSessionActive;
            string clientReference = currentStatus.ClientReference;

            var transactionVendorPayload = BuildTransactionVendorPayload(e.TransactionInfo, e.TransactionFilePath, clientReference);

            if (e.BonusInfo != null)
                UpdateBonusInfo(e.BonusInfo, transactionVendorPayload);

            if (isOutsideTerminalManagerSession || !isSessionActive)
            {
                UpdateCurrentStatus(new TerminalStatus
                {
                    State = TerminalState.Error,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationType = OperationType.None,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = $"Unexpected transaction result received outside session. MessageId={e.TransactionInfo?.MessageId}. Manual reconciliation may be required.",
                    OperationResult = new OperationResult
                    {
                        VendorPayload = transactionVendorPayload,
                    },
                    Fault = new FaultInfo
                    {
                        Code = "UnexpectedTransactionResult",
                        Message = $"Transaction result received outside managed session. MessageId={e.TransactionInfo?.MessageId}",
                    },
                });
            }
            else
            {
                AddSessionStatus(new TerminalStatus
                {
                    State = TerminalState.Completed,
                    IsConnected = currentStatus.IsConnected,
                    LastResultIsFinal = true,
                    ActiveOperationId = currentStatus.ActiveOperationId,
                    ActiveOperationType = OperationType.None,
                    ClientReference = clientReference,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Message = "Transaction completed.",
                    OperationResult = new OperationResult
                    {
                        VendorPayload = transactionVendorPayload,
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(e.TransactionFilePath))
                DeleteTransactionFile(e.TransactionFilePath);
        }

        /// <inheritdoc/>
        public bool ShowUserPromptDialog(string promptMessage)
        {
            Trace.WriteLine($"{nameof(ShowUserPromptDialog)}: {promptMessage}", GetType().FullName);
            return WaitForPromptReply(promptMessage, inputSpec: null, userInput: out _);
        }

        /// <inheritdoc/>
        public bool ShowUserPromptDialog(string promptMessage, out string userInput)
        {
            Trace.WriteLine($"{nameof(ShowUserPromptDialog)}: {promptMessage}", GetType().FullName);
            return WaitForPromptReply(promptMessage, inputSpec: new PromptInputSpec { Kind = PromptInputSpecKind.Digits, MinLength = 4, MaxLength = 6 }, userInput: out userInput);
        }

        /// <summary>
        /// Publishes an <see cref="TerminalState.AwaitingPrompt"/> currentStatus and blocks the calling
        /// thread until the POS responds via <see cref="DoRespondToPromptAsync"/> or
        /// <see cref="PaymentTerminalBase.PromptTimeoutSeconds"/> elapses.
        /// Returns <c>false</c> on timeout or decline.
        /// </summary>
        /// <param name="promptMessage">The message to display to the operator.</param>
        /// <param name="inputSpec">
        /// Input constraints for free-form entry. When <c>null</c> the prompt is treated as yes/no only.
        /// </param>
        /// <param name="userInput">
        /// On return, contains the operator-entered text when <paramref name="inputSpec"/> is non-null;
        /// otherwise <c>null</c>.
        /// </param>
        private bool WaitForPromptReply(string promptMessage, PromptInputSpec inputSpec, out string userInput)
        {
            string promptId = "pr_" + Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddSeconds(PromptTimeoutSeconds);

            var prompt = new Prompt
            {
                PromptId = promptId,
                Message = promptMessage,
                YesNo = inputSpec == null,
                Input = inputSpec,
                CreatedAt = now,
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
                UpdatedAt = now,
                Message = promptMessage,
                Prompt = prompt,
            });

            bool accepted = pending.Wait(PromptTimeoutSeconds * 1000, out userInput);
            pendingPrompt = null;
            return accepted;
        }

        /// <summary>
        /// Cancels any prompt currently blocking the ECR protocol thread by signalling it to decline.
        /// Safe to call when no prompt is active.
        /// </summary>
        private void CancelPendingPrompt()
        {
            pendingPrompt?.Cancel();
        }

        /// <inheritdoc/>
        protected override Task<TerminalStatus> DoGetTerminalStatusAsync(CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoGetTerminalStatusAsync)}", GetType().FullName);

            // Fire the currentStatus request to the hardware — the result arrives asynchronously
            // via TerminalManager_TransactionStatusChanged, which updates CurrentStatus directly.
            // Return the currentStatus known currentStatus immediately so the caller is never blocked.
            TerminalManager.RequestTerminalStatus();

            return Task.FromResult(CurrentStatus);
        }

        /// <inheritdoc/>
        protected override async Task DoAbortAsync(CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoAbortAsync)}", GetType().FullName);

            var activeOperationType = CurrentStatus.ActiveOperationType;

            if (activeOperationType == OperationType.LoyaltyActivate
                || activeOperationType == OperationType.LoyaltyDeactivate)
            {
                // Loyalty abort — deactivate bonus card mode synchronously.
                // Base class handles all status updates for both session and non-session paths.
                await TerminalManager.WaitForCommandAcceptanceAsync(
                    CommandId.DisableBonusCardMode,
                    () => TerminalManager.DisableBonusCardMode(),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Transaction abort — fire and forget. Result arrives via
                // AbortTransactionResultReceived which calls AddSessionStatus.
                TerminalManager.AbortTransaction();
            }
        }

        /// <inheritdoc/>
        protected override async Task DoStartLoyaltyActivateAsync(LoyaltyActivateRequest request, CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoStartLoyaltyActivateAsync)}: {request}", GetType().FullName);

            await TerminalManager.WaitForCommandAcceptanceAsync(
                CommandId.EnableBonusCardMode,
                () => TerminalManager.EnableBonusCardMode(request?.Mode == LoyaltyActivateRequestMode.Autoreply),
                cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override async Task DoStartLoyaltyDeactivateAsync(BaseActionRequest request, CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoStartLoyaltyDeactivateAsync)}: {request}", GetType().FullName);

            await TerminalManager.WaitForCommandAcceptanceAsync(
                CommandId.DisableBonusCardMode,
                () => TerminalManager.DisableBonusCardMode(),
                cancellationToken).ConfigureAwait(false);
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
        protected override Task<OperationAccepted> DoStartPurchaseAsync(PurchaseRequest request, CancellationToken cancellationToken = default)
        {
            Trace.WriteLine($"{nameof(DoStartPurchaseAsync)}: {request}", GetType().FullName);

            TerminalManager.RunPayment(
                MoneyConversions.MinorUnitsToDecimal(request.Amount, request.Currency),
                request.IsLoyaltyHandled,
                request.ClientReference);

            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = $"Purchase {request.Amount} {request.Currency} started.",
            });
        }

        /// <inheritdoc/>
        protected override Task<OperationAccepted> DoStartRefundAsync(RefundRequest request, CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoStartRefundAsync)}: {request}", GetType().FullName);

            TerminalManager.Refund(
                MoneyConversions.MinorUnitsToDecimal(request.Amount, request.Currency),
                request.ClientReference);

            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = $"Refund {request.Amount} started.",
            });
        }

        /// <inheritdoc/>
        protected override Task<OperationAccepted> DoStartReversalAsync(ReversalRequest request, CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoStartReversalAsync)}: {request}", GetType().FullName);

            TerminalManager.Reversal(
                request.TransactionId,
                request.Timestamp.UtcDateTime,
                request.ClientReference);

            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = $"Reversal of transaction '{request.TransactionId}' started.",
            });
        }

        /// <inheritdoc/>
        public override Task<TerminalSettings> GetTerminalSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new TerminalSettings
                {
                    VendorPayload = VendorPayload
                        ?? new VendorPayload
                        {
                            AdditionalProperties = new Dictionary<string, object>
                            {
                                { TraceSerialBytesPropertyName, TraceSerialBytes },
                                { EnableManualAuthorizationPropertyName, EnableManualAuthorization }
                            }
                        }
                });
        }

        /// <inheritdoc/>
        protected override async Task<OperationAccepted> DoTestConnectionAsync(CancellationToken cancellationToken)
        {
            Trace.WriteLine($"{nameof(DoTestConnectionAsync)}", GetType().FullName);

            await TerminalManager.WaitForCommandAcceptanceAsync(
                CommandId.TestTerminal,
                () => TerminalManager.TestTerminal(),
                cancellationToken).ConfigureAwait(false);

            return new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = $"Terminal connection '{Connection.Serial.PortName}' test completed.",
            };
        }

        /// <inheritdoc/>
        protected override Task<OperationAccepted> DoReleaseAsync(CancellationToken cancellationToken)
        {
            RemoveTerminalManagerEvents();
            CancelPendingPrompt();

            try
            {
                TerminalManager.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(DoReleaseAsync)}: failed to dispose terminal manager: {ex}", GetType().FullName);
            }

            return Task.FromResult(new OperationAccepted
            {
                OperationId = Guid.NewGuid().ToString(),
                Message = "Terminal released.",
            });
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Trace.WriteLine($"{nameof(Dispose)}:{disposing}", GetType().FullName);

            if (disposing)
            {
                RemoveTerminalManagerEvents();
                CancelPendingPrompt();

                try
                {
                    TerminalManager.Dispose();
                }
                catch
                {
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Holds state for a prompt blocking the ECR protocol thread while awaiting
        /// a REST response delivered via <see cref="DoRespondToPromptAsync"/>.
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