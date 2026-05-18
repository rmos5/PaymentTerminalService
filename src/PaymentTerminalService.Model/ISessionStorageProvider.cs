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
        /// Persists an unrecognized terminal status that arrived outside of any known session context.
        /// The status is wrapped in a synthetic session and written to an orphan file for
        /// manual review or reconciliation.
        /// </summary>
        /// <param name="status">The unrecognized terminal status to persist.</param>
        void SaveOrphan(TerminalStatus status);

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
        /// Loads all orphan sessions from storage.
        /// Orphan sessions contain unrecognized terminal statuses that arrived outside of any known
        /// session context and require manual review or reconciliation.
        /// </summary>
        /// <returns>All orphan session responses found in storage.</returns>
        IEnumerable<TerminalSessionResponse> LoadOrphanSessions();

        /// <summary>
        /// Marks a completed session as confirmed by renaming its storage file.
        /// </summary>
        /// <param name="sessionName">The session storage key.</param>
        void ConfirmSession(string sessionName);
    }
}