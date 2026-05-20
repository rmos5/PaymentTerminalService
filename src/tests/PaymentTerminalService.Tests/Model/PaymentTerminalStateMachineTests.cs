using Microsoft.VisualStudio.TestTools.UnitTesting;
using PaymentTerminalService.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentTerminalService.Tests.Model
{
    [TestClass]
    public class PaymentTerminalStateMachineTests
    {
        private static FakePaymentTerminal MakeTerminal(bool isLoyaltySupported = false) =>
            new FakePaymentTerminal("terminal-1", "Test Terminal", isLoyaltySupported);

        private static TerminalStatus MakeStatus(
            TerminalState state,
            OperationType opType = OperationType.None,
            bool isFinal = false,
            string operationId = null) => new TerminalStatus
            {
                State = state,
                ActiveOperationType = opType,
                ActiveOperationId = operationId ?? Guid.NewGuid().ToString(),
                LastResultIsFinal = isFinal,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

        [TestMethod]
        public async Task Purchase_WhenIdle_Succeeds()
        {
            var terminal = MakeTerminal();
            await terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 100 });
            Assert.IsTrue(terminal.IsPaymentSessionActive);
        }

        [TestMethod]
        public async Task Purchase_WhenSessionActive_Throws()
        {
            var terminal = MakeTerminal();
            await terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 100 });
            await Assert.ThrowsExceptionAsync<ApiConflictException>(() =>
                terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 200 }));
        }

        [TestMethod]
        public async Task Purchase_WhenBusy_Throws()
        {
            var terminal = MakeTerminal();
            terminal.SimulateStatusUpdate(MakeStatus(TerminalState.TransactionInProgress));
            await Assert.ThrowsExceptionAsync<ApiConflictException>(() =>
                terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 100 }));
        }

        [TestMethod]
        public async Task Purchase_FromLoyaltyActivateAwaitingResult_Succeeds()
        {
            var terminal = MakeTerminal(isLoyaltySupported: true);
            terminal.SimulateStatusUpdate(MakeStatus(TerminalState.AwaitingResult, OperationType.LoyaltyActivate));
            await terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 100 });
            Assert.IsTrue(terminal.IsPaymentSessionActive);
        }

        [TestMethod]
        public async Task Abort_WhenIdleAfterAbort_NoSession_Throws()
        {
            var terminal = MakeTerminal();
            terminal.SimulateStatusUpdate(MakeStatus(TerminalState.Idle, OperationType.Abort));
            await Assert.ThrowsExceptionAsync<ApiConflictException>(() =>
                terminal.AbortTransactionAsync(new AbortTransactionRequest()));
        }

        [TestMethod]
        public async Task Abort_WhenSessionActive_Succeeds()
        {
            var terminal = MakeTerminal();
            await terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 100 });
            var result = await terminal.AbortTransactionAsync(new AbortTransactionRequest());
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task LoyaltyActivate_WhenNotSupported_Throws()
        {
            var terminal = MakeTerminal(isLoyaltySupported: false);
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                terminal.LoyaltyActivateAsync());
        }

        [TestMethod]
        public async Task LoyaltyActivate_WhenSessionActive_Throws()
        {
            var terminal = MakeTerminal(isLoyaltySupported: true);
            await terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 100 });
            await Assert.ThrowsExceptionAsync<ApiConflictException>(() =>
                terminal.LoyaltyActivateAsync());
        }

        [TestMethod]
        public async Task LoyaltyDeactivate_WhenNotSupported_Throws()
        {
            var terminal = MakeTerminal(isLoyaltySupported: false);
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                terminal.LoyaltyDeactivateAsync());
        }

        [TestMethod]
        public async Task Refund_WhenNotSupported_Throws()
        {
            var terminal = MakeTerminal();
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                terminal.StartRefundAsync(new RefundRequest { Amount = 100 }));
        }

        [TestMethod]
        public async Task Reversal_WhenNotSupported_Throws()
        {
            var terminal = MakeTerminal();
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                terminal.StartReversalAsync(new ReversalRequest
                {
                    TransactionId = "txn-1",
                    Timestamp = DateTimeOffset.UtcNow,
                }));
        }

        [TestMethod]
        public async Task AddSessionStatus_FinalState_EndsSession()
        {
            var terminal = MakeTerminal();
            await terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 100 });
            Assert.IsTrue(terminal.IsPaymentSessionActive);

            var final = MakeStatus(TerminalState.Idle, OperationType.None, isFinal: true,
                operationId: terminal.CurrentStatus.ActiveOperationId);
            terminal.SimulateSessionUpdate(final);

            Assert.IsFalse(terminal.IsPaymentSessionActive);
        }

        [TestMethod]
        public async Task AddSessionStatus_NonFinalState_SessionRemainsActive()
        {
            var terminal = MakeTerminal();
            await terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 100 });

            var nonFinal = MakeStatus(TerminalState.TransactionInProgress, OperationType.Purchase, isFinal: false,
                operationId: terminal.CurrentStatus.ActiveOperationId);
            terminal.SimulateSessionUpdate(nonFinal);

            Assert.IsTrue(terminal.IsPaymentSessionActive);
        }

        [TestMethod]
        public async Task AddSessionStatus_StaleOperationId_StatusDiscarded()
        {
            var terminal = MakeTerminal();
            await terminal.StartPurchaseAsync(new PurchaseRequest { Amount = 100 });
            var activeOperationId = terminal.CurrentStatus.ActiveOperationId;

            var stale = MakeStatus(TerminalState.Idle, OperationType.None, isFinal: true,
                operationId: "stale-operation-id");
            terminal.SimulateSessionUpdate(stale);

            Assert.IsTrue(terminal.IsPaymentSessionActive);
            Assert.AreEqual(activeOperationId, terminal.CurrentStatus.ActiveOperationId);
        }

        [TestMethod]
        public void UpdateCurrentStatus_ErrorState_SetsIsTerminalTestRequired()
        {
            var terminal = MakeTerminal();
            Assert.IsFalse(terminal.IsTerminalTestRequired);
            terminal.SimulateStatusUpdate(MakeStatus(TerminalState.Error));
            Assert.IsTrue(terminal.IsTerminalTestRequired);
        }

        [TestMethod]
        public void UpdateCurrentStatus_IdleAfterError_ClearsIsTerminalTestRequired()
        {
            var terminal = MakeTerminal();
            terminal.SimulateStatusUpdate(MakeStatus(TerminalState.Error));
            Assert.IsTrue(terminal.IsTerminalTestRequired);
            terminal.SimulateStatusUpdate(MakeStatus(TerminalState.Idle));
            Assert.IsFalse(terminal.IsTerminalTestRequired);
        }
    }

    internal class FakePaymentTerminal : PaymentTerminalBase
    {
        public FakePaymentTerminal(string terminalId, string displayName, bool isLoyaltySupported = false)
            : base(terminalId, displayName,
                  new TerminalConnectionOption { ConnectionId = "conn-1", ConnectionType = ConnectionType.Serial, DisplayName = "Test" },
                  isLoyaltySupported,
                  null,
                  new NullSessionStorageProvider())
        { }

        public override string Vendor => "FakeVendor";
        public override string Model => "FakeModel";

        public void SimulateStatusUpdate(TerminalStatus status) => UpdateCurrentStatus(status);
        public void SimulateSessionStart(TerminalStatus status) => StartSession(status);
        public void SimulateSessionUpdate(TerminalStatus status) => AddSessionStatus(status);

        protected override Task DoAbortTransactionAsync(AbortTransactionRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;
        protected override Task DoStartLoyaltyActivateAsync(LoyaltyActivateRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;
        protected override Task DoStartLoyaltyDeactivateAsync(BaseActionRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;
        public override Task<TerminalSettings> GetTerminalSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new TerminalSettings());
        protected override Task<TerminalStatus> DoGetTerminalStatusAsync(CancellationToken cancellationToken)
            => Task.FromResult(CurrentStatus);
        protected override Task<OperationAccepted> DoReleaseAsync(CancellationToken cancellationToken)
            => Task.FromResult(new OperationAccepted { OperationId = Guid.NewGuid().ToString() });
        protected override Task<OperationAccepted> DoRespondToPromptAsync(PromptResponseRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new OperationAccepted { OperationId = Guid.NewGuid().ToString() });
        protected override Task<OperationAccepted> DoStartPurchaseAsync(PurchaseRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new OperationAccepted { OperationId = Guid.NewGuid().ToString() });
        protected override Task<OperationAccepted> DoStartRefundAsync(RefundRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new OperationAccepted { OperationId = Guid.NewGuid().ToString() });
        protected override Task<OperationAccepted> DoStartReversalAsync(ReversalRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new OperationAccepted { OperationId = Guid.NewGuid().ToString() });
        protected override Task<OperationAccepted> DoTestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new OperationAccepted { OperationId = Guid.NewGuid().ToString() });
    }

    internal class NullSessionStorageProvider : ISessionStorageProvider
    {
        public void SaveSession(TerminalSessionResponse item) { }
        public void SaveOrphan(TerminalStatus status) { }
        public IEnumerable<TerminalSessionResponse> LoadOngoingSessions() => Array.Empty<TerminalSessionResponse>();
        public IEnumerable<TerminalSessionResponse> LoadCompletedSessions() => Array.Empty<TerminalSessionResponse>();
        public IEnumerable<TerminalSessionResponse> LoadFailedSessions() => Array.Empty<TerminalSessionResponse>();
        public IEnumerable<TerminalSessionResponse> LoadConfirmedSessions() => Array.Empty<TerminalSessionResponse>();
        public IEnumerable<TerminalSessionResponse> LoadOrphanSessions() => Array.Empty<TerminalSessionResponse>();
        public void ConfirmSession(string sessionName) { }
    }
}
