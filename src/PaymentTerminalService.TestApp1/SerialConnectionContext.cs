namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Context class for serial connection settings, supporting property change notification and busy state.
    /// </summary>
    public class SerialConnectionContext : ContextBase
    {
        private string portName;

        /// <summary>
        /// Gets or sets the serial port name.
        /// Notifies property changes and model state updates.
        /// </summary>
        public string PortName
        {
            get => portName;
            set
            {
                if (portName != value)
                {
                    portName = value;
                    UpdateModelState();
                }
            }
        }
    }
}
