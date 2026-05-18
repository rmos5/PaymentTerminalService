using PaymentTerminalService.Model;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Context class for a payment terminal descriptor, used for UI binding and state management.
    /// Inherits from <see cref="ContextBase"/> to provide property change notification and busy state.
    /// </summary>
    public partial class TerminalDescriptorContext : ContextBase
    {
        /// <summary>
        /// Gets or sets the unique terminal identifier.
        /// Notifies property changes and model state updates.
        /// </summary>
        public string TerminalId { get; internal set; }

        /// <summary>
        /// Gets or sets the display name for the terminal.
        /// Intended for UI display. Set internally by the data factory.
        /// </summary>
        public string DisplayName { get; internal set; }

        /// <summary>
        /// Gets or sets the terminal model name.
        /// Intended for UI display. Set internally by the data factory.
        /// </summary>
        public string Model { get; internal set; }

        public string Vendor { get; internal set; }

        public string Version { get; internal set; }

        public bool IsLoyaltySupported { get; set; }

        public VendorPayload VendorPayload { get; set; }

        public IList<TerminalConnectionOptionContext> ConnectionContexts { get; internal set; } = new ObservableCollection<TerminalConnectionOptionContext>();

        private TerminalConnectionOptionContext selectedConnectionContext;

        public TerminalConnectionOptionContext SelectedConnectionContext
        {
            get => selectedConnectionContext;
            set
            {
                if (selectedConnectionContext != value)
                {
                    selectedConnectionContext = value;
                    // Update the selected connection ID when the context changes
                    OnPropertyChanged(nameof(SelectedConnectionContext));
                    OnPropertyChanged(nameof(SelectedConnectionId));
                }
            }
        }

        public string SelectedConnectionId => SelectedConnectionContext?.ConnectionId;

        public override string ToString()
        {
            return $"{TerminalId} {DisplayName}";
        }
    }
}
