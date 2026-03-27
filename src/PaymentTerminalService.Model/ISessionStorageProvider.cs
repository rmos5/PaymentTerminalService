using System.Collections.Generic;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Defines a session storage provider for persisting and loading terminal sessions.
    /// </summary>
    public interface ISessionStorageProvider
    {
        /// <summary>
        /// Saves a terminal session response, assigning a session name if one is not already set.
        /// The assigned name is written back to <paramref name="item"/> for use in subsequent saves.
        /// </summary>
        /// <param name="item">The terminal session response to persist.</param>
        void SaveSession(TerminalSessionResponse item);

        /// <summary>
        /// Loads all ongoing sessions from storage.
        /// Ongoing sessions were persisted but never reached a final state, typically due to a
        /// process restart or unexpected termination. Use this on startup to detect and recover
        /// interrupted transactions.
        /// </summary>
        /// <returns>All ongoing session responses found in storage.</returns>
        IEnumerable<TerminalSessionResponse> LoadOngoingSessions();

        /// <summary>
        /// Loads all successfully completed sessions from storage.
        /// </summary>
        /// <returns>All completed session responses found in storage.</returns>
        IEnumerable<TerminalSessionResponse> LoadCompletedSessions();

        /// <summary>
        /// Loads all failed sessions from storage.
        /// Failed sessions reached a final state with a fault and require manual review
        /// or reconciliation.
        /// </summary>
        /// <returns>All failed session responses found in storage.</returns>
        IEnumerable<TerminalSessionResponse> LoadFailedSessions();

        /// <summary>
        /// Loads all confirmed sessions from storage.
        /// Confirmed sessions have been acknowledged by the caller via
        /// <see cref="ConfirmSession"/> and are candidates for batch cleanup.
        /// </summary>
        /// <returns>All confirmed session responses found in storage.</returns>
        IEnumerable<TerminalSessionResponse> LoadConfirmedSessions();

        /// <summary>
        /// Marks a completed session as confirmed by renaming its storage file.
        /// </summary>
        /// <param name="sessionName">The session storage key.</param>
        void ConfirmSession(string sessionName);
    }
}