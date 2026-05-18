using PaymentTerminalService.Model;
using System.Linq;

namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Provides generic and type-specific static methods for converting model objects to context objects for UI binding,
    /// and for converting context objects back to model DTOs.
    /// Uses reflection to copy property values by name for generic conversions, and supports custom logic for special cases.
    /// </summary>
    public static class DataFactory
    {
        private static void ToContext(SerialConnection serial, out SerialConnectionContext context)
        {
            context = new SerialConnectionContext
            {
                PortName = serial.PortName
            };
        }

        private static void ToContext(TcpConnection tcp, out TcpConnectionContext context)
        {
            context = new TcpConnectionContext
            {
                Host = tcp.Host,
                Port = tcp.Port
            };
        }

        private static void ToContext(BluetoothConnection bluetooth, out BluetoothConnectionContext context)
        {
            context = new BluetoothConnectionContext
            {
                MacAddress = bluetooth.MacAddress
            };
        }

        private static void ToContext(TerminalConnectionOption connection, out TerminalConnectionOptionContext item)
        {
            item = new TerminalConnectionOptionContext
            {
                ConnectionId = connection.ConnectionId,
                ConnectionType = connection.ConnectionType,
                DisplayName = connection.DisplayName,
                VendorPayload = connection.VendorPayload
            };

            switch (item.ConnectionType)
            {
                case ConnectionType.Serial:
                    ToContext(connection.Serial, out SerialConnectionContext serialConnectionContext);
                    item.SerialConnection = serialConnectionContext;
                    break;
                case ConnectionType.Tcp:
                    ToContext(connection.Tcp, out TcpConnectionContext tcpConnectionContext);
                    item.TcpConnection = tcpConnectionContext;
                    break;
                case ConnectionType.Bluetooth:
                    ToContext(connection.Bluetooth, out BluetoothConnectionContext bluetoothConnectionContext);
                    item.BluetoothConnection = bluetoothConnectionContext;
                    break;
                default:
                    break;
            }
        }

        internal static void ToContext(TerminalDescriptor terminal, out TerminalDescriptorContext context)
        {
            context = new TerminalDescriptorContext
            {
                TerminalId = terminal.TerminalId,
                DisplayName = terminal.DisplayName,
                Model = terminal.Model,
                Vendor = terminal.Vendor,
                Version = terminal.Version,
                IsLoyaltySupported = terminal.IsLoyaltySupported,
                VendorPayload = terminal.VendorPayload
            };

            foreach (var connection in terminal.Connections)
            {
                TerminalConnectionOptionContext connectionContext;
                ToContext(connection, out connectionContext);
                context.ConnectionContexts.Add(connectionContext);
            }

            if (terminal.SelectedConnectionId != null)
            {
                context.SelectedConnectionContext = context.ConnectionContexts.FirstOrDefault(c => c.ConnectionId == terminal.SelectedConnectionId);
            }
        }

        /// <summary>
        /// Converts a <see cref="SerialConnectionContext"/> context object to a <see cref="SerialConnection"/> DTO.
        /// </summary>
        /// <param name="context">The context instance to convert.</param>
        /// <param name="serial">The output DTO populated with property values from the context.</param>
        public static void ToDto(SerialConnectionContext context, out SerialConnection serial)
        {
            serial = context == null ? null : new SerialConnection
            {
                PortName = context.PortName
            };
        }

        /// <summary>
        /// Converts a <see cref="TcpConnectionContext"/> context object to a <see cref="TcpConnection"/> DTO.
        /// </summary>
        /// <param name="context">The context instance to convert.</param>
        /// <param name="tcp">The output DTO populated with property values from the context.</param>
        public static void ToDto(TcpConnectionContext context, out TcpConnection tcp)
        {
            tcp = context == null ? null : new TcpConnection
            {
                Host = context.Host,
                Port = context.Port
            };
        }

        /// <summary>
        /// Converts a <see cref="BluetoothConnectionContext"/> context object to a <see cref="BluetoothConnection"/> DTO.
        /// </summary>
        /// <param name="context">The context instance to convert.</param>
        /// <param name="bluetooth">The output DTO populated with property values from the context.</param>
        public static void ToDto(BluetoothConnectionContext context, out BluetoothConnection bluetooth)
        {
            bluetooth = context == null ? null : new BluetoothConnection
            {
                MacAddress = context.MacAddress
            };
        }

        /// <summary>
        /// Converts a <see cref="TerminalConnectionOptionContext"/> context object to a <see cref="TerminalConnectionOption"/> DTO.
        /// </summary>
        /// <param name="context">The context instance to convert.</param>
        /// <param name="connection">The output DTO populated with property values from the context.</param>
        public static void ToDto(TerminalConnectionOptionContext context, out TerminalConnectionOption connection)
        {
            connection = context == null ? null : new TerminalConnectionOption
            {
                ConnectionId = context.ConnectionId,
                ConnectionType = context.ConnectionType,
                DisplayName = context.DisplayName,
                VendorPayload = context.VendorPayload
            };

            switch (connection?.ConnectionType)
            {
                case ConnectionType.Serial:
                    ToDto(context.SerialConnection, out SerialConnection serial);
                    connection.Serial = serial;
                    break;
                case ConnectionType.Tcp:
                    ToDto(context.TcpConnection, out TcpConnection tcp);
                    connection.Tcp = tcp;
                    break;
                case ConnectionType.Bluetooth:
                    ToDto(context.BluetoothConnection, out BluetoothConnection bluetooth);
                    connection.Bluetooth = bluetooth;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Converts a <see cref="TerminalDescriptorContext"/> context object to a <see cref="TerminalDescriptor"/> DTO.
        /// </summary>
        /// <param name="context">The context instance to convert.</param>
        /// <param name="terminal">The output DTO populated with property values from the context.</param>
        public static void ToDto(TerminalDescriptorContext context, out TerminalDescriptor terminal)
        {
            terminal = context == null ? null : new TerminalDescriptor
            {
                TerminalId = context.TerminalId,
                DisplayName = context.DisplayName,
                Model = context.Model,
                SelectedConnectionId = context.SelectedConnectionId,
                IsLoyaltySupported = context.IsLoyaltySupported,
                VendorPayload = context.VendorPayload
            };

            foreach (var connectionContext in context.ConnectionContexts)
            {
                ToDto(connectionContext, out TerminalConnectionOption connection);
                terminal.Connections.Add(connection);
            }
        }
    }
}
