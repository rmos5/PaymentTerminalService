using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace PaymentTerminalService.Model.Tests
{
    [TestClass]
    public class ApiModelsEqualityTests
    {
        // ─── Helpers ────────────────────────────────────────────────────────────────

        private static readonly DateTimeOffset Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private static VendorPayload EmptyPayload()
        {
            return new VendorPayload();
        }

        private static OperationResult CreateOperationResult()
        {
            return new OperationResult { VendorPayload = EmptyPayload() };
        }

        private static PromptInputSpec CreatePromptInputSpec(
            PromptInputSpecKind kind = PromptInputSpecKind.Alphanumeric,
            int minLength = 0,
            int maxLength = 10,
            string hint = "hint")
        {
            return new PromptInputSpec
            {
                Kind = kind,
                MinLength = minLength,
                MaxLength = maxLength,
                Hint = hint
            };
        }

        private static Prompt CreatePrompt(string promptId = "p1", string message = "msg")
        {
            return new Prompt
            {
                PromptId = promptId,
                Message = message,
                CreatedAt = Epoch,
                ExpiresAt = Epoch
            };
        }

        private static TerminalStatus CreateStatus(
            TerminalState state = TerminalState.Idle,
            bool isConnected = true)
        {
            return new TerminalStatus
            {
                State = state,
                IsConnected = isConnected,
                UpdatedAt = Epoch
            };
        }

        private static SerialConnection CreateSerial(string portName = "COM1")
        {
            return new SerialConnection { PortName = portName };
        }

        private static TcpConnection CreateTcp(string host = "localhost", int port = 8080)
        {
            return new TcpConnection { Host = host, Port = port };
        }

        private static BluetoothConnection CreateBluetooth(string mac = "AA:BB:CC:DD:EE:FF")
        {
            return new BluetoothConnection { MacAddress = mac };
        }

        private static TerminalConnectionOption CreateConnectionOption(
            string connectionId = "conn1",
            ConnectionType type = ConnectionType.Tcp)
        {
            return new TerminalConnectionOption
            {
                ConnectionId = connectionId,
                ConnectionType = type,
                DisplayName = "Test"
            };
        }

        private static TerminalDescriptor CreateDescriptor(string terminalId = "T1")
        {
            return new TerminalDescriptor
            {
                TerminalId = terminalId,
                Vendor = "Acme",
                IsLoyaltySupported = false
            };
        }

        private static TerminalSessionResponse CreateSession(string sessionName = "session-1")
        {
            return new TerminalSessionResponse { SessionName = sessionName };
        }

        // ─── OperationResult ────────────────────────────────────────────────────────

        [TestMethod]
        public void OperationResult_SameReference_ReturnsTrue()
        {
            var result = CreateOperationResult();

            Assert.IsTrue(result.Equals(result));
        }

        [TestMethod]
        public void OperationResult_EqualContent_ReturnsTrue()
        {
            var first  = CreateOperationResult();
            var second = CreateOperationResult();

            Assert.IsTrue(first.Equals(second));
        }

        [TestMethod]
        public void OperationResult_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreateOperationResult().Equals((OperationResult)null));
        }

        [TestMethod]
        public void OperationResult_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreateOperationResult().GetHashCode(), CreateOperationResult().GetHashCode());
        }

        // ─── FaultInfo ───────────────────────────────────────────────────────────────

        [TestMethod]
        public void FaultInfo_SameReference_ReturnsTrue()
        {
            var fault = new FaultInfo { Code = "ERR", Message = "msg" };

            Assert.IsTrue(fault.Equals(fault));
        }

        [TestMethod]
        public void FaultInfo_EqualContent_ReturnsTrue()
        {
            var first  = new FaultInfo { Code = "ERR", Message = "msg" };
            var second = new FaultInfo { Code = "ERR", Message = "msg" };

            Assert.IsTrue(first.Equals(second));
        }

        [TestMethod]
        public void FaultInfo_DifferentCode_ReturnsFalse()
        {
            var first  = new FaultInfo { Code = "ERR1", Message = "msg" };
            var second = new FaultInfo { Code = "ERR2", Message = "msg" };

            Assert.IsFalse(first.Equals(second));
        }

        [TestMethod]
        public void FaultInfo_DifferentMessage_ReturnsFalse()
        {
            Assert.IsFalse(
                new FaultInfo { Code = "ERR", Message = "msg1" }.Equals(
                new FaultInfo { Code = "ERR", Message = "msg2" }));
        }

        [TestMethod]
        public void FaultInfo_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(new FaultInfo { Code = "ERR", Message = "msg" }.Equals((FaultInfo)null));
        }

        [TestMethod]
        public void FaultInfo_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            var first  = new FaultInfo { Code = "ERR", Message = "msg" };
            var second = new FaultInfo { Code = "ERR", Message = "msg" };

            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
        }

        [TestMethod]
        public void FaultInfo_NullFields_GetHashCode_DoesNotThrow()
        {
            _ = new FaultInfo().GetHashCode();
        }

        [TestMethod]
        public void FaultInfo_GetHashCode_DifferentCode_ReturnDifferentHashCodes()
        {
            var first  = new FaultInfo { Code = "ERR1", Message = "msg" };
            var second = new FaultInfo { Code = "ERR2", Message = "msg" };

            Assert.AreNotEqual(first.GetHashCode(), second.GetHashCode());
        }

        // ─── PromptInputSpec ────────────────────────────────────────────────────────

        [TestMethod]
        public void PromptInputSpec_SameReference_ReturnsTrue()
        {
            var spec = CreatePromptInputSpec();

            Assert.IsTrue(spec.Equals(spec));
        }

        [TestMethod]
        public void PromptInputSpec_EqualContent_ReturnsTrue()
        {
            Assert.IsTrue(CreatePromptInputSpec().Equals(CreatePromptInputSpec()));
        }

        [TestMethod]
        public void PromptInputSpec_DifferentKind_ReturnsFalse()
        {
            var first  = CreatePromptInputSpec(kind: PromptInputSpecKind.Alphanumeric);
            var second = CreatePromptInputSpec(kind: PromptInputSpecKind.Digits);

            Assert.IsFalse(first.Equals(second));
        }

        [TestMethod]
        public void PromptInputSpec_KindNone_EqualContent_ReturnsTrue()
        {
            var first  = CreatePromptInputSpec(kind: PromptInputSpecKind.None);
            var second = CreatePromptInputSpec(kind: PromptInputSpecKind.None);

            Assert.IsTrue(first.Equals(second));
        }

        [TestMethod]
        public void PromptInputSpec_KindAny_EqualContent_ReturnsTrue()
        {
            var first  = CreatePromptInputSpec(kind: PromptInputSpecKind.Any);
            var second = CreatePromptInputSpec(kind: PromptInputSpecKind.Any);

            Assert.IsTrue(first.Equals(second));
        }

        [TestMethod]
        public void PromptInputSpec_KindNone_VsKindAny_ReturnsFalse()
        {
            Assert.IsFalse(
                CreatePromptInputSpec(kind: PromptInputSpecKind.None).Equals(
                CreatePromptInputSpec(kind: PromptInputSpecKind.Any)));
        }

        [TestMethod]
        public void PromptInputSpec_DifferentHint_ReturnsFalse()
        {
            Assert.IsFalse(CreatePromptInputSpec(hint: "a").Equals(CreatePromptInputSpec(hint: "b")));
        }

        [TestMethod]
        public void PromptInputSpec_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreatePromptInputSpec().Equals((PromptInputSpec)null));
        }

        [TestMethod]
        public void PromptInputSpec_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreatePromptInputSpec().GetHashCode(), CreatePromptInputSpec().GetHashCode());
        }

        [TestMethod]
        public void PromptInputSpec_GetHashCode_DifferentKind_ReturnDifferentHashCodes()
        {
            var first  = CreatePromptInputSpec(kind: PromptInputSpecKind.Digits);
            var second = CreatePromptInputSpec(kind: PromptInputSpecKind.Alphanumeric);

            Assert.AreNotEqual(first.GetHashCode(), second.GetHashCode());
        }

        [TestMethod]
        public void PromptInputSpec_NullHint_GetHashCode_DoesNotThrow()
        {
            var spec = new PromptInputSpec { Kind = PromptInputSpecKind.None };
            _ = spec.GetHashCode();
        }

        // ─── Prompt ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Prompt_SameReference_ReturnsTrue()
        {
            var prompt = CreatePrompt();

            Assert.IsTrue(prompt.Equals(prompt));
        }

        [TestMethod]
        public void Prompt_EqualContent_ReturnsTrue()
        {
            Assert.IsTrue(CreatePrompt().Equals(CreatePrompt()));
        }

        [TestMethod]
        public void Prompt_DifferentPromptId_ReturnsFalse()
        {
            Assert.IsFalse(CreatePrompt(promptId: "p1").Equals(CreatePrompt(promptId: "p2")));
        }

        [TestMethod]
        public void Prompt_DifferentMessage_ReturnsFalse()
        {
            Assert.IsFalse(CreatePrompt(message: "a").Equals(CreatePrompt(message: "b")));
        }

        [TestMethod]
        public void Prompt_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreatePrompt().Equals((Prompt)null));
        }

        [TestMethod]
        public void Prompt_NullFields_GetHashCode_DoesNotThrow()
        {
            var prompt = new Prompt();
            _ = prompt.GetHashCode();
        }

        [TestMethod]
        public void Prompt_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreatePrompt().GetHashCode(), CreatePrompt().GetHashCode());
        }

        // ─── TerminalStatus ──────────────────────────────────────────────────────────

        [TestMethod]
        public void TerminalStatus_SameReference_ReturnsTrue()
        {
            var status = CreateStatus();

            Assert.IsTrue(status.Equals(status));
        }

        [TestMethod]
        public void TerminalStatus_EqualContent_ReturnsTrue()
        {
            Assert.IsTrue(CreateStatus().Equals(CreateStatus()));
        }

        [TestMethod]
        public void TerminalStatus_DifferentState_ReturnsFalse()
        {
            Assert.IsFalse(CreateStatus(state: TerminalState.Idle).Equals(CreateStatus(state: TerminalState.Error)));
        }

        [TestMethod]
        public void TerminalStatus_DifferentIsConnected_ReturnsFalse()
        {
            Assert.IsFalse(CreateStatus(isConnected: true).Equals(CreateStatus(isConnected: false)));
        }

        [TestMethod]
        public void TerminalStatus_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreateStatus().Equals((TerminalStatus)null));
        }

        [TestMethod]
        public void TerminalStatus_NullFields_GetHashCode_DoesNotThrow()
        {
            var status = new TerminalStatus();
            _ = status.GetHashCode();
        }

        [TestMethod]
        public void TerminalStatus_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreateStatus().GetHashCode(), CreateStatus().GetHashCode());
        }

        // ─── SerialConnection ────────────────────────────────────────────────────────

        [TestMethod]
        public void SerialConnection_EqualContent_ReturnsTrue()
        {
            Assert.IsTrue(CreateSerial().Equals(CreateSerial()));
        }

        [TestMethod]
        public void SerialConnection_DifferentPortName_ReturnsFalse()
        {
            Assert.IsFalse(CreateSerial("COM1").Equals(CreateSerial("COM2")));
        }

        [TestMethod]
        public void SerialConnection_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreateSerial().Equals((SerialConnection)null));
        }

        [TestMethod]
        public void SerialConnection_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreateSerial().GetHashCode(), CreateSerial().GetHashCode());
        }

        [TestMethod]
        public void SerialConnection_GetHashCode_DifferentPortName_ReturnDifferentHashCodes()
        {
            Assert.AreNotEqual(CreateSerial("COM1").GetHashCode(), CreateSerial("COM2").GetHashCode());
        }

        [TestMethod]
        public void SerialConnection_NullPortName_GetHashCode_DoesNotThrow()
        {
            _ = new SerialConnection().GetHashCode();
        }

        // ─── TcpConnection ───────────────────────────────────────────────────────────

        [TestMethod]
        public void TcpConnection_EqualContent_ReturnsTrue()
        {
            Assert.IsTrue(CreateTcp().Equals(CreateTcp()));
        }

        [TestMethod]
        public void TcpConnection_DifferentHost_ReturnsFalse()
        {
            Assert.IsFalse(CreateTcp(host: "host1").Equals(CreateTcp(host: "host2")));
        }

        [TestMethod]
        public void TcpConnection_DifferentPort_ReturnsFalse()
        {
            Assert.IsFalse(CreateTcp(port: 8080).Equals(CreateTcp(port: 9090)));
        }

        [TestMethod]
        public void TcpConnection_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreateTcp().Equals((TcpConnection)null));
        }

        [TestMethod]
        public void TcpConnection_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreateTcp().GetHashCode(), CreateTcp().GetHashCode());
        }

        [TestMethod]
        public void TcpConnection_GetHashCode_DifferentHost_ReturnDifferentHashCodes()
        {
            Assert.AreNotEqual(CreateTcp("host1").GetHashCode(), CreateTcp("host2").GetHashCode());
        }

        [TestMethod]
        public void TcpConnection_GetHashCode_DifferentPort_ReturnDifferentHashCodes()
        {
            Assert.AreNotEqual(CreateTcp(port: 8080).GetHashCode(), CreateTcp(port: 9090).GetHashCode());
        }

        [TestMethod]
        public void TcpConnection_NullHost_GetHashCode_DoesNotThrow()
        {
            _ = new TcpConnection().GetHashCode();
        }

        // ─── BluetoothConnection ─────────────────────────────────────────────────────

        [TestMethod]
        public void BluetoothConnection_EqualContent_ReturnsTrue()
        {
            Assert.IsTrue(CreateBluetooth().Equals(CreateBluetooth()));
        }

        [TestMethod]
        public void BluetoothConnection_DifferentMac_ReturnsFalse()
        {
            Assert.IsFalse(CreateBluetooth("AA:BB:CC:DD:EE:FF").Equals(CreateBluetooth("11:22:33:44:55:66")));
        }

        [TestMethod]
        public void BluetoothConnection_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreateBluetooth().Equals((BluetoothConnection)null));
        }

        [TestMethod]
        public void BluetoothConnection_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreateBluetooth().GetHashCode(), CreateBluetooth().GetHashCode());
        }

        [TestMethod]
        public void BluetoothConnection_GetHashCode_DifferentMac_ReturnDifferentHashCodes()
        {
            Assert.AreNotEqual(
                CreateBluetooth("AA:BB:CC:DD:EE:FF").GetHashCode(),
                CreateBluetooth("11:22:33:44:55:66").GetHashCode());
        }

        [TestMethod]
        public void BluetoothConnection_NullMac_GetHashCode_DoesNotThrow()
        {
            _ = new BluetoothConnection().GetHashCode();
        }

        // ─── TerminalConnectionOption ─────────────────────────────────────────────────

        [TestMethod]
        public void TerminalConnectionOption_EqualContent_ReturnsTrue()
        {
            Assert.IsTrue(CreateConnectionOption().Equals(CreateConnectionOption()));
        }

        [TestMethod]
        public void TerminalConnectionOption_DifferentConnectionId_ReturnsFalse()
        {
            Assert.IsFalse(CreateConnectionOption("conn1").Equals(CreateConnectionOption("conn2")));
        }

        [TestMethod]
        public void TerminalConnectionOption_DifferentConnectionType_ReturnsFalse()
        {
            Assert.IsFalse(
                CreateConnectionOption(type: ConnectionType.Tcp).Equals(
                CreateConnectionOption(type: ConnectionType.Serial)));
        }

        [TestMethod]
        public void TerminalConnectionOption_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreateConnectionOption().Equals((TerminalConnectionOption)null));
        }

        [TestMethod]
        public void TerminalConnectionOption_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreateConnectionOption().GetHashCode(), CreateConnectionOption().GetHashCode());
        }

        [TestMethod]
        public void TerminalConnectionOption_GetHashCode_DifferentConnectionId_ReturnDifferentHashCodes()
        {
            Assert.AreNotEqual(
                CreateConnectionOption("conn1").GetHashCode(),
                CreateConnectionOption("conn2").GetHashCode());
        }

        [TestMethod]
        public void TerminalConnectionOption_NullFields_GetHashCode_DoesNotThrow()
        {
            _ = new TerminalConnectionOption().GetHashCode();
        }

        // ─── TerminalDescriptor ───────────────────────────────────────────────────────

        [TestMethod]
        public void TerminalDescriptor_EqualContent_ReturnsTrue()
        {
            Assert.IsTrue(CreateDescriptor().Equals(CreateDescriptor()));
        }

        [TestMethod]
        public void TerminalDescriptor_DifferentTerminalId_ReturnsFalse()
        {
            Assert.IsFalse(CreateDescriptor("T1").Equals(CreateDescriptor("T2")));
        }

        [TestMethod]
        public void TerminalDescriptor_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreateDescriptor().Equals((TerminalDescriptor)null));
        }

        [TestMethod]
        public void TerminalDescriptor_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreateDescriptor().GetHashCode(), CreateDescriptor().GetHashCode());
        }

        [TestMethod]
        public void TerminalDescriptor_GetHashCode_DifferentTerminalId_ReturnDifferentHashCodes()
        {
            Assert.AreNotEqual(CreateDescriptor("T1").GetHashCode(), CreateDescriptor("T2").GetHashCode());
        }

        [TestMethod]
        public void TerminalDescriptor_NullFields_GetHashCode_DoesNotThrow()
        {
            _ = new TerminalDescriptor().GetHashCode();
        }

        // ─── TerminalSessionResponse ──────────────────────────────────────────────────

        [TestMethod]
        public void TerminalSessionResponse_EqualContent_ReturnsTrue()
        {
            Assert.IsTrue(CreateSession().Equals(CreateSession()));
        }

        [TestMethod]
        public void TerminalSessionResponse_DifferentSessionName_ReturnsFalse()
        {
            Assert.IsFalse(CreateSession("s1").Equals(CreateSession("s2")));
        }

        [TestMethod]
        public void TerminalSessionResponse_DifferentStatuses_ReturnsFalse()
        {
            var first  = CreateSession();
            var second = CreateSession();
            second.Statuses.Add(CreateStatus());

            Assert.IsFalse(first.Equals(second));
        }

        [TestMethod]
        public void TerminalSessionResponse_NullOther_ReturnsFalse()
        {
            Assert.IsFalse(CreateSession().Equals((TerminalSessionResponse)null));
        }

        [TestMethod]
        public void TerminalSessionResponse_GetHashCode_EqualInstances_ReturnSameHashCode()
        {
            Assert.AreEqual(CreateSession().GetHashCode(), CreateSession().GetHashCode());
        }

        [TestMethod]
        public void TerminalSessionResponse_GetHashCode_DifferentSessionName_ReturnDifferentHashCodes()
        {
            Assert.AreNotEqual(CreateSession("s1").GetHashCode(), CreateSession("s2").GetHashCode());
        }

        [TestMethod]
        public void TerminalSessionResponse_NullFields_GetHashCode_DoesNotThrow()
        {
            _ = new TerminalSessionResponse().GetHashCode();
        }
    }
}