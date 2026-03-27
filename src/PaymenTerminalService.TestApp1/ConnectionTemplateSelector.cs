using PaymentTerminalService.Model;
using System.Windows;
using System.Windows.Controls;

namespace PaymentTerminalService.TestApp1
{
    public class ConnectionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SerialTemplate { get; set; }
        public DataTemplate TcpTemplate { get; set; }
        public DataTemplate BluetoothTemplate { get; set; }
        public DataTemplate DefaultTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            DataTemplate result = DefaultTemplate;
            TerminalConnectionOptionContext context = item as TerminalConnectionOptionContext;
            if (context != null)
            {
                switch (context.ConnectionType)
                {
                    case ConnectionType.Serial:
                        result = SerialTemplate;
                        break;
                    case ConnectionType.Tcp:
                        result = TcpTemplate;
                        break;
                    case ConnectionType.Bluetooth:
                        result = BluetoothTemplate;
                        break;
                }
            }

            return result;
        }
    }
}