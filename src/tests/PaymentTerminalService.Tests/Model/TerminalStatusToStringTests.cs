using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PaymentTerminalService.Model.Tests
{
    [TestClass]
    public class TerminalStatusToStringTests
    {
        [TestMethod]
        public void ToString_MinimalStatus_ContainsStateConnectedAndFinal()
        {
            var status = new TerminalStatus
            {
                State = TerminalState.Idle,
                IsConnected = true,
                LastResultIsFinal = false
            };

            var result = status.ToString();

            StringAssert.Contains(result, "State=Idle");
            StringAssert.Contains(result, "Connected=True");
            StringAssert.Contains(result, "Final=False");
        }

        [TestMethod]
        public void ToString_WithMessage_ContainsMessage()
        {
            var status = new TerminalStatus { State = TerminalState.Idle, Message = "Processing" };

            StringAssert.Contains(status.ToString(), "Msg=\"Processing\"");
        }

        [TestMethod]
        public void ToString_WithoutMessage_DoesNotContainMsgKey()
        {
            var status = new TerminalStatus { State = TerminalState.Idle };

            Assert.IsFalse(status.ToString().Contains("Msg="));
        }

        [TestMethod]
        public void ToString_WithActiveOperation_ContainsOpType()
        {
            var status = new TerminalStatus
            {
                State = TerminalState.TransactionInProgress,
                ActiveOperationType = OperationType.Purchase,
                ActiveOperationId = "op-123"
            };

            var result = status.ToString();

            StringAssert.Contains(result, "Op=Purchase");
            StringAssert.Contains(result, "OpId=op-123");
        }

        [TestMethod]
        public void ToString_WithFault_ContainsFaultCodeAndMessage()
        {
            var status = new TerminalStatus
            {
                State = TerminalState.Error,
                Fault = new FaultInfo { Code = "TerminalError", Message = "Connection lost" }
            };

            StringAssert.Contains(status.ToString(), "Fault=TerminalError: Connection lost");
        }

        [TestMethod]
        public void ToString_WithoutFault_DoesNotContainFaultKey()
        {
            var status = new TerminalStatus { State = TerminalState.Idle };

            Assert.IsFalse(status.ToString().Contains("Fault="));
        }

        [TestMethod]
        public void ToString_OperationTypeNone_DoesNotContainOpKey()
        {
            var status = new TerminalStatus
            {
                State = TerminalState.Idle,
                ActiveOperationType = OperationType.None
            };

            Assert.IsFalse(status.ToString().Contains("Op="));
        }
    }
}