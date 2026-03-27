using System;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Builds session file names using a timestamp-based naming scheme.
    /// Base session names are derived from the current UTC time using <see cref="TimestampFormat"/>
    /// (for example: <c>2026-03-05_14-22-10.123</c>).
    /// The <c>.ptss</c> extension stands for Payment Terminal Service Session.
    /// <para>
    /// Each session progresses through file name states as it advances through its lifecycle:
    /// <list type="table">
    /// <listheader><term>State</term><term>Example file name</term></listheader>
    /// <item><term>Ongoing</term><description><c>_2026-03-05_14-22-10.123.ptss</c></description></item>
    /// <item><term>Completed</term><description><c>2026-03-05_14-22-10.123.ptss</c></description></item>
    /// <item><term>Failed</term><description><c>2026-03-05_14-22-10.123.failed.ptss</c></description></item>
    /// <item><term>Confirmed</term><description><c>2026-03-05_14-22-10.123.confirmed.ptss</c></description></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class TimestampFileNameProvider : ISessionNameProvider
    {
        /// <summary>
        /// Timestamp format used when generating a new base session name from the current UTC time.
        /// Produces names such as <c>2026-03-05_14-22-10.123</c>.
        /// </summary>
        public const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss.fff";

        /// <summary>
        /// Prefix prepended to the ongoing session file name.
        /// Allows ongoing files to be visually and programmatically distinguished from
        /// completed and failed files in the session directory.
        /// </summary>
        public const string OngoingPrefix = "_";

        /// <summary>
        /// Suffix inserted before <see cref="FileExtension"/> for failed session file names.
        /// </summary>
        public const string FailedSuffix = ".failed";

        /// <summary>
        /// Suffix inserted before <see cref="FileExtension"/> for confirmed session file names.
        /// </summary>
        public const string ConfirmedSuffix = ".confirmed";

        /// <summary>
        /// File extension appended to all session file names.
        /// Stands for Payment Terminal Service Session.
        /// </summary>
        public const string FileExtension = ".ptss";

        /// <inheritdoc/>
        /// <remarks>
        /// When <paramref name="sessionName"/> is null or whitespace a new bare session name is
        /// generated from the current UTC timestamp. Otherwise the existing name is returned unchanged.
        /// The returned value carries no prefix or file extension and is suitable as input to
        /// <see cref="GetOngoingSessionFileName"/>, <see cref="GetCompletedSessionFileName"/>, 
        /// <see cref="GetFailedSessionFileName"/>, and <see cref="GetConfirmedSessionFileName"/>.
        /// </remarks>
        public string GetOrCreateSessionName(string sessionName)
        {
            return string.IsNullOrWhiteSpace(sessionName)
                ? DateTimeOffset.UtcNow.ToString(TimestampFormat)
                : sessionName;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Prepends <see cref="OngoingPrefix"/> and appends <see cref="FileExtension"/> to the
        /// bare <paramref name="sessionName"/>.
        /// </remarks>
        public string GetOngoingSessionFileName(string sessionName)
        {
            if (string.IsNullOrWhiteSpace(sessionName))
                throw new ArgumentNullException(nameof(sessionName));

            return OngoingPrefix + sessionName + FileExtension;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Appends <see cref="FileExtension"/> to the bare <paramref name="sessionName"/>.
        /// </remarks>
        public string GetCompletedSessionFileName(string sessionName)
        {
            if (string.IsNullOrWhiteSpace(sessionName))
                throw new ArgumentNullException(nameof(sessionName));

            return sessionName + FileExtension;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Appends <see cref="FailedSuffix"/> and <see cref="FileExtension"/> to the bare
        /// <paramref name="sessionName"/>.
        /// </remarks>
        public string GetFailedSessionFileName(string sessionName)
        {
            if (string.IsNullOrWhiteSpace(sessionName))
                throw new ArgumentNullException(nameof(sessionName));

            return sessionName + FailedSuffix + FileExtension;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Appends <see cref="ConfirmedSuffix"/> and <see cref="FileExtension"/> to the bare
        /// <paramref name="sessionName"/>.
        /// </remarks>
        public string GetConfirmedSessionFileName(string sessionName)
        {
            if (string.IsNullOrWhiteSpace(sessionName))
                throw new ArgumentNullException(nameof(sessionName));

            return sessionName + ConfirmedSuffix + FileExtension;
        }
    }
}