using System.ComponentModel;

namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Context class for TCP connection settings, supporting property change notification and busy state.
    /// </summary>
    public class TcpConnectionContext : ContextBase
    {
        private string host;
        private int port;

        /// <summary>
        /// Gets or sets the TCP host address.
        /// Notifies property changes and model state updates.
        /// </summary>
        public string Host
        {
            get => host;
            set
            {
                if (host != value)
                {
                    host = value;
                    UpdateModelState();
                }
            }
        }

        /// <summary>
        /// Gets or sets the TCP port number.
        /// Notifies property changes and model state updates.
        /// </summary>
        public int Port
        {
            get => port;
            set
            {
                if (port != value)
                {
                    port = value;
                    UpdateModelState();
                }
            }
        }
    }
}
