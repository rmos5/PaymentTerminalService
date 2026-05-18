namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Context class for Bluetooth connection settings, supporting property change notification and busy state.
    /// </summary>
    public class BluetoothConnectionContext : ContextBase
    {
        private string macAddress;

        /// <summary>
        /// Gets or sets the Bluetooth MAC address.
        /// Notifies property changes and model state updates.
        /// </summary>
        public string MacAddress
        {
            get => macAddress;
            set
            {
                if (macAddress != value)
                {
                    macAddress = value;
                    UpdateModelState();
                }
            }
        }
    }
}
