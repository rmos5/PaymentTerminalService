using PaymentTerminalService.Model;

namespace PaymentTerminalService.TestApp1
{
    public partial class TerminalConnectionOptionContext : ContextBase
    {
        public string DisplayName { get; internal set; }

        public ConnectionType ConnectionType { get; internal set; }

        public string ConnectionId { get; internal set; }

        public SerialConnectionContext SerialConnection { get; internal set; }

        public TcpConnectionContext TcpConnection { get; internal set; }

        public BluetoothConnectionContext BluetoothConnection { get; internal set; }

        public VendorPayload VendorPayload { get; set; }
    }
}
