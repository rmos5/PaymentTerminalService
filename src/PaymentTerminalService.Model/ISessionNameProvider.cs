namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Provides session names across lifecycle states.
    /// Implementations may apply storage-specific or custom naming heuristics.
    /// </summary>
    public interface ISessionNameProvider
    {
        /// <summary>
        /// Gets an existing session name or creates a new one when the base name is null or empty.
        /// </summary>
        /// <param name="sessionName">The base session name, or null to create a new one.</param>
        /// <returns>The bare session name with no prefix or file extension.</returns>
        string GetOrCreateSessionName(string sessionName);

        /// <summary>
        /// Gets the file name for an ongoing session.
        /// Ongoing sessions use a distinguishing prefix so they are visually separated
        /// from completed and failed sessions on disk.
        /// </summary>
        /// <param name="sessionName">The bare session name.</param>
        /// <returns>The ongoing session file name.</returns>
        string GetOngoingSessionFileName(string sessionName);

        /// <summary>
        /// Gets the file name for a completed session.
        /// </summary>
        /// <param name="sessionName">The bare session name.</param>
        /// <returns>The completed session file name.</returns>
        string GetCompletedSessionFileName(string sessionName);

        /// <summary>
        /// Gets the file name for a failed session.
        /// </summary>
        /// <param name="sessionName">The bare session name.</param>
        /// <returns>The failed session file name.</returns>
        string GetFailedSessionFileName(string sessionName);

        /// <summary>
        /// Gets the file name for a confirmed session.
        /// </summary>
        /// <param name="sessionName">The bare session name.</param>
        /// <returns>The confirmed session file name.</returns>
        string GetConfirmedSessionFileName(string sessionName);

        /// <summary>
        /// Gets the file name for an orphan session.
        /// Orphan files hold raw unrecognized data that arrived outside of any known session context.
        /// </summary>
        /// <param name="sessionName">The bare session name.</param>
        /// <returns>The orphan session file name.</returns>
        string GetOrphanSessionFileName(string sessionName);
    }
}