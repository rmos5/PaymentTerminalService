using System.Threading;
using System.Threading.Tasks;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Provides terminal discovery, selection, and access to the currently selected payment terminal.
    /// Responsible for listing available terminals, retrieving the current selection,
    /// and activating a terminal implementation for use.
    /// </summary>
    public interface IPaymentTerminalSelector
    {
        /// <summary>
        /// Gets the currently selected and activated payment terminal instance, or null if none is selected.
        /// </summary>
        IPaymentTerminal SelectedPaymentTerminal { get; }

        /// <summary>
        /// Discovers all available payment terminals and their connection options.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="TerminalCatalogResponse"/> containing the list of discovered terminals,
        /// their connection options, and the currently selected terminal.
        /// </returns>
        Task<TerminalCatalogResponse> GetTerminalsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the currently selected and activated terminal, including its connection details.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="SelectedTerminalResponse"/> describing the selected terminal and connection.
        /// </returns>
        Task<SelectedTerminalResponse> GetSelectedTerminalAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Selects and activates a terminal implementation using the given connection.
        /// Optionally applies initial settings as part of the activation.
        /// </summary>
        /// <param name="request">The selection request, including terminal and connection details.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="SelectedTerminalResponse"/> describing the newly selected and activated terminal.
        /// </returns>
        Task<SelectedTerminalResponse> SelectTerminalAsync(
            SelectTerminalRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases the currently selected terminal, deactivating its implementation and freeing resources.
        /// Returns an OperationAccepted result to correlate the release operation.
        /// </summary>
        /// <returns>
        /// A <see cref="OperationAccepted"/> indicating the release operation was accepted.
        /// </returns>
        Task<OperationAccepted> ReleaseSelectedTerminalAsync();
    }
}